## Kerberos Authentication Proxy

## Ingress

When configured as reverse proxy (or route service on Cloud Foundry), it will forces all requests to be Kerberos authenticated before request is passed downstream. The underlying user identity will be extracted from Kerberos ticket and passed to the application via as X-CF-Identity header. Any downstream application can use this header to make assertions on user's identity. .NET applications that use `hwc_buildpack` can leverage [Service Auth Buildpack](https://github.com/macsux/route-service-auth-buildpack) which will install an HTTP Module to automatically extract X-CF-Identity and establish a thread ClaimsPrincipal by the time the user code starts running. When dealing with WCF apps, set upstream calling app to use transport security with client credentials. Disable WCF security in downstream app as the thread identity will already be established by the module injected by the buildpack.

This implementation relies on fully managed Kerberos ticket parser provided via [Kerberos.NET](	<https://github.com/SteveSyfuhs/Kerberos.NET>) library and does not require any communication with the domain controller. **The only requirement to make this work is to provide route service the SPN password**

### How to use

Route service requires password of the service principal for which the ticket is intended. This must be supplied either via PRINCIPAL_PASSWORD environmental variable or specifying path to keytab containing the credentials for the principal via KRB5_CLIENT_KTNAME environmental variable
- Push with included manifest
- On the downstream app, create a CUPS with route service URL and bind it to the the application route

### AD Roles

Kerberos tickets contain AD groups the user belongs to, but they are in [SID](https://en.wikipedia.org/wiki/Security_Identifier) format, and are not sent downstream by default. If you want to make assertions on roles, you need to configure LDAP for the route service which will be used to map SIDs to their common names. When configured it will send roles via `X-CF-Roles` in comma delimited format. Groups are loaded once and cached in dictionary, so no LDAP requests are made per request. You can configure LDAP settings either via `appsettings.json` or by setting the following environmental variables:

- `LDAP__SERVER` - IP or hostname of LDAP server
- `LDAP__PORT` - port of LDAP server. Defaults to 389
- `LDAP__USERNAME` - username to authenticate against LDAP
- `LDAP__PASSWORD` - password to authenticate against LDAP
- `LDAP__GROUPSQUERY` - LDAP query that targets the root container of groups that need to be resolved. For example: `CN=Users,DC=almirex,DC=dc` 
- `LDAP__FILTER` - filter to apply on each object. Defaults to `(objectClass=group)`

### Whitelisting

It is sometimes desirable to leave open certain routes to be accessible without logging in. You can use `Whitelist:Paths` section to configure which URLs will be excluded from requiring to be authenticated. Ex

```
"Whitelist": {
  "Paths": [
    "/cloudfoundryapplication",
    "/actuator"
  ]
},
```

## Egress

The proxy can be used to create egress routes to specific endpoint that will attach a SPNEGO Kerberos ticket to every http request. Each route configuration is mapped to a unique port on the proxy. For example when configured, a call to `http://proxy:10001` to proxy will forward the request to `http://google.com` with SPNEGO header. Because there is no authentication for backend services, this mode should only be used when calls to the proxy egress ports can be secured through other means, such as firewall and routing rules. The most common usage for this is to use it as a sidecar in Kubernetes pod, making app talk to the sidecar instead of final route. **Do not enable this feature unless your infrastructure setup can guarantee that only authorized calling app can access these ports on the proxy**

### How to use

Setup egress map in configuration as following:

```
"ProxyMap": {
  "Entries": [{
    "ListenPort": 10001,
    "TargetUrl": "http://www.google.com",
    "ClientLogin": "iwaclient@ALMIREX.DC",
    "ClientPassword": "MYPASSWORD",
    "TargetSpn": "iwasvc@ALMIREX.DC"
    }
  ]
}
```

- `ListenPort` - the port on proxy which will forward the requests to target
- `TargetUrl` - the base URL where to forward the requests
- `ClientLogin` - the Kerberos user under of the caller identity
- `ClientPassword` - the Kerberos password for the caller identity
- `TargetSpn` - target SPN to obtain for the ticket in valid SPN format: `http/fqdn`  

## How to build

- Install [.NET Core 3.1 SDK](<https://dotnet.microsoft.com/download>)
- Run `dotnet publish` in src directory

## Troubleshooting

- Ensure that Service principal you're using has the appropriate SPN associated with it. For apps that use user's browser as the client, set spn to `http/fqdn`, for WCF apps set to `host/fqdn`

- If testing via browser, ensure that the site is allowed to perform negotiate authentication. For IE & Chrome this is done by adding site to Trusted list in security settings. For Firefox, open up `about:config` and set fqdn in `network.negotiate-auth.trusted-uris`

- Ensure you're testing from domain joined machine

  

## Testing on Non domain joined box
Normally when using browser on domain joined machine, a Kerberos ticket will be automatically obtained by the browser from the OS and attached to the header. You can however craft a manual request with Postman. To do so, set the following headers:

- `X-CF-Forwarded-Url` - the URL of where the proxy should forward the request. This is your backend app
- `Authentication` - `Negotiate <KerberosTicketAsBase64>`

You can obtain a base64 ticket via util project. From terminal set inside `src\KerberosUtil`, run this command:

`dotnet run get-ticket --kdc ADSERVER --user myuser@domain.com --password PASSWORD --spn http/someapp.domain.io`

