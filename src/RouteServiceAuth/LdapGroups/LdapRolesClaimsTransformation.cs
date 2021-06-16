using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Novell.Directory.Ldap;
using RouteServiceAuth.Proxy;
using RouteServiceAuth.Proxy.Configuration;

namespace RouteServiceAuth.LdapGroups
{
    /// <summary>
    /// Transforms current security principal by converting SIDs to AD role names. Mapping is loaded on startup from LDAP 
    /// </summary>
    public class LdapRolesClaimsTransformer : IStartupFilter, IClaimsTransformation
    {
        private readonly ReaderWriterLockSlim _lock = new();
        private readonly IOptionsMonitor<LdapOptions> _options;
        private readonly IOptionsMonitor<Dictionary<string,Secret>> _credentials;
        private readonly IValidator<LdapOptions> _optionsValidator;
        private readonly ILogger<LdapRolesClaimsTransformer> _logger;
        private Dictionary<string, string> _sidsToGroupNames = new();
        private Dictionary<string, List<string>> _groupSidHierarchy = new();
        private DateTime _lastRefreshTime;
        private Timer? _refreshTimer;
        

        public LdapRolesClaimsTransformer(
            IOptionsMonitor<LdapOptions> options, 
            IOptionsMonitor<Dictionary<string,Secret>>  credentials, 
            IValidator<LdapOptions> optionsValidator,
            ILogger<LdapRolesClaimsTransformer> logger)
        {
            _options = options;
            _credentials = credentials;
            _optionsValidator = optionsValidator;
            _logger = logger;
        }

        private void Initialize() => OnConfigChange();

        private void OnConfigChange()
        {
            _refreshTimer?.Dispose();
            try
            {
                var options = _options.CurrentValue;
                RefreshGroups(options);
                _refreshTimer = new Timer(_ => CheckGroupChanges(), null, options.RefreshFrequency, options.RefreshFrequency);
            }
            catch (OptionsValidationException e)
            {
                
                _logger.LogWarning("AD group principal enrichment is disabled because LDAP options isn't properly configured.\n{Error}", string.Join("\n",e.Failures));
            }
        }

        private void RefreshGroups(LdapOptions options)
        {
            
            try
            {
                
                using var cn = GetConnection();
                
                var attributes = new[]{"objectSid", "sAMAccountName", "distinguishedName","memberOf"};
                var query = new SearchOptions(options.GroupsQuery!, LdapConnection.ScopeSub, _options.CurrentValue.Filter, attributes, false, new LdapSearchConstraints());
                var groups = cn.SearchUsingSimplePaging(query, 1000);
                _lastRefreshTime = DateTime.UtcNow;
                var dc = Regex.Match(options.GroupsQuery!,@"DC=.+").Value;
                var builtinQuery = $"CN=Builtin,{dc}";
                var builtinGroups = cn.SearchUsingSimplePaging(new SearchOptions(builtinQuery, LdapConnection.ScopeSub, _options.CurrentValue.Filter, attributes, false, new LdapSearchConstraints()), 1000);
                var simpleGroups = groups
                    .Concat(builtinGroups)
                    .Select(x => new SimpleGroup
                    {
                        Sid = x.GetSidString(),
                        sAMAccountName = x.GetAttribute("sAMAccountName").StringValue,
                        DistinguishedName = x.GetAttribute("distinguishedName").StringValue,
                        MemberOfDNs = x.GetStringArray("memberOf")
                    })
                    .ToList();
                var groupsByDn = simpleGroups.ToDictionary(x => x.DistinguishedName);
                var loadedDNs = new HashSet<string>(groupsByDn.Keys);
                // algo summary:
                // 1. create dictionary of group to hashset of belongsTo
                // 2. create a dictionary of group to ALL parent hashsets it maintains (from step 1)
                // 3. flatten hashsets into a single Dictionary<string,HashSet<string>> and remove self from belongTo
                // 4. convert dictionary to sid based mapping for use at runtime
                // this algo is necessary because groups can have a circular relationship, which can cause stack burst if hashset buckets are not used
                
                // 1
                var directGroups = simpleGroups.ToDictionary(x => x.DistinguishedName, x =>
                {
                    // filter out member names we haven't loaded from ldap
                    var memberDNs = new HashSet<string>(x.MemberOfDNs);
                    memberDNs.IntersectWith(loadedDNs);
                    return memberDNs;
                });

                // 2
                var groupBelongToSet = new Dictionary<string, HashSet<HashSet<string>>>();
                HashSet<HashSet<string>> ResolveGroupHierarchy(SimpleGroup group)
                {
                    if (groupBelongToSet.TryGetValue(group.DistinguishedName, out var result))
                        return result;
                    result = new() {directGroups[group.DistinguishedName]};
                    groupBelongToSet.Add(group.DistinguishedName, result);
                    foreach (var memberOfDn in group.MemberOfDNs)
                    {
                        if (!groupsByDn.TryGetValue(memberOfDn, out var memberGroup))
                            continue; // this group is listed as memberOf, but we haven't loaded it as part of LDAP (probably part of some other OU). skip
                        if(!groupBelongToSet.TryGetValue(memberOfDn, out var inheritedGroups))
                        {
                            inheritedGroups = ResolveGroupHierarchy(memberGroup);
                        }
                        result.UnionWith(inheritedGroups);
                    }
                    
                    return result;
                }
                foreach (var group in simpleGroups)
                {
                    ResolveGroupHierarchy(group);
                }

                // 3
                var groupDnHierarchy = groupBelongToSet.ToDictionary(x => x.Key, x => x.Value.Aggregate(new HashSet<string>(), (accumulator, bucket) =>
                {
                    accumulator.UnionWith(bucket);
                    return accumulator;
                }));
                
                foreach (var (group, belongsTo) in groupDnHierarchy)
                {
                    belongsTo.Remove(group);
                }

                try
                {
                    AcquireWriteLock();
                    _sidsToGroupNames = simpleGroups.ToDictionary(x => x.Sid, x => x.sAMAccountName);
                    // 4. convert groupHierarchy to sid based mapping
                    _groupSidHierarchy = groupDnHierarchy.ToDictionary(kv => groupsByDn[kv.Key].Sid, kv => kv.Value.Select(x => groupsByDn[x].Sid).ToList());
                    int groupCount = _sidsToGroupNames.Count;
                    _logger.LogInformation("Loaded {GroupCount} groups from LDAP", groupCount);
                    
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
            catch (Exception e)
            {
                throw new AuthenticationException("Failed to load groups from LDAP", e);
            }
        }

        public void CheckGroupChanges()
        {
            try
            {
                var options = _options.CurrentValue;
                var updatesFilter = $"(&{options.Filter}(whenChanged>={_lastRefreshTime:yyyyMMddHHmmss}.0Z))";
                var attributes = new[] {"objectSid"};
                _logger.LogTrace("Checking if LDAP groups have changed");
                using var cn = GetConnection();
                var query = new SearchOptions(options.GroupsQuery!, LdapConnection.ScopeSub, updatesFilter, attributes, false, new LdapSearchConstraints());
                var isUpdated = cn.SearchUsingSimplePaging(query, 1).Any();
                if (isUpdated)
                {
                    _logger.LogInformation("Detected changes to LDAP groups since last refresh");
                    RefreshGroups(options);
                }
            }
            catch (OptionsValidationException)
            {
                // ignore (validation will be handled by callback of config change)
            }
            
        }

        private LdapConnection GetConnection()
        {
            if (!_credentials.CurrentValue.TryGetSecretOfType<WindowsCredential>(_options.CurrentValue.CredentialId, out var credential))
                throw new InvalidOperationException($"Referenced credential id {_options.CurrentValue.CredentialId} not found in Credentials collection");
            var cn = new LdapConnection();
            cn.Connect(_options.CurrentValue.Server, _options.CurrentValue.Port);
            cn.Bind(credential.Username, credential.Password);
            return cn;
        }

        private void AcquireWriteLock()
        {
            while (!_lock.TryEnterWriteLock(TimeSpan.FromMilliseconds(100)))
            {
                Thread.Yield();
            }
        }
        private void AcquireReadLock()
        {
            while (!_lock.TryEnterReadLock(TimeSpan.FromMilliseconds(100)))
            {
                Thread.Yield();
            }
        }

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return builder =>
            {
                Initialize();
                _options.OnChange(_ => Initialize());
                next(builder);
            };
        }

        public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            if (principal.Identity == null)
                return Task.FromResult(principal);
            AcquireReadLock();
            try
            {
                var identity = (ClaimsIdentity) principal.Identity;
                var claimsToAdd = identity!.Claims
                    .Where(x => x.Type == ClaimTypes.GroupSid)
                    .Select(x => x.Value)
                    .SelectMany(sid =>
                    {
                        if (!_groupSidHierarchy.TryGetValue(sid, out var belongToSids))
                            belongToSids = new List<string>();
                        var allGroupSids = belongToSids.Union(new[] {sid});
                        return allGroupSids;
                    })
                    .Distinct()
                    .Select(sid => _sidsToGroupNames.GetValueOrDefault(sid))
                    .Where(groupName => groupName != null)
                    .Where(groupName => !identity.HasClaim(ClaimTypes.Role, groupName!))
                    .Select(groupName => new Claim(ClaimTypes.Role, groupName!))
                    .ToList();
                identity.AddClaims(claimsToAdd);

                // remove all sid based claims
                var claimsToRemove = identity!.Claims.Where(x => x.Type == ClaimTypes.GroupSid).ToArray();
                foreach (var claim in claimsToRemove)
                {
                    identity.RemoveClaim(claim);
                }
                return Task.FromResult(principal);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        struct SimpleGroup
        {
            public string Sid { get; set; }
            // ReSharper disable once InconsistentNaming
            public string sAMAccountName { get; set; }
            public string DistinguishedName { get; set; }
            public string[] MemberOfDNs { get; set; }
        }
    }
}