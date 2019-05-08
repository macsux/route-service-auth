## Route Service enabling Kerberos Authentication

This route service forces all requests to be Kerberos authenticated before request is passed downstream. The underlying user identity will be extracted from Kerberos ticket and passed to the application via X-CF-Identity header. Any downstream .NET applications downstream of this route service that are started via hwc_buildpack can leverage [Service Auth Buildpack](https://github.com/macsux/route-service-auth-buildpack) which will install an HTTP Module to automatically extract X-CF-Identity and establish a thread ClaimsPrincipal by the time the user code starts running. When dealing with WCF apps, set upstream calling app to use transport security with client credentials. Disable WCF security in downstream app as the thread identity will already be established by buildpack.

Additionally when authentication is established, a session cookie will be issued to prevent requiring caller to obtain new Kerberos tickets as they are usually very short lived. Note that default behavior for cookie protection is to use DPAPI, if scaling out one must either set machine key or switch to alternate data protection implementation (such as [Steeltoe Redis key storage provider](<http://steeltoe.io/docs/steeltoe-security/#4-0-redis-key-storage-provider>)) to ensure all instances can decrypt the cookie.

This implementation relies on fully managed Kerberos ticket parser provided via [Kerberos.NET](	<https://github.com/SteveSyfuhs/Kerberos.NET>) library and does not require any communication with the domain controller. **The only requirement to make this work is to provide route service the SPN password**



## How to use
Route service requires password of the service principal for which the ticket is intended. This must be supplied either via PRINCIPAL_PASSWORD environmental variable or specifying path to keytab containing the credentials for the principal via KRB5_CLIENT_KTNAME environmental variable
- Push with included manifest
- On the downstream app, create a CUPS with route service URL and bind it to the the application route

## Troubleshooting

- Ensure that Service principal you're using has the appropriate SPN associated with it. For apps that use user's browser as the client, set spn to `http/fqdn`, for WCF apps set to `host/fqdn`
- If testing via browser, ensure that the site is allowed to perform negotiate authentication. For IE & Chrome this is done by adding site to Trusted list in security settings. For Firefox, open up `about:config` and set fqdn in `network.negotiate-auth.trusted-uris`
- Ensure you're testing from domain joined machine