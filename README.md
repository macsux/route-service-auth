## Kerberos Authentication Proxy

This project allows solving for Kerberos authentication scenarios when ticket is transmitted via HTTP Negotiate header (aka Integrated Windows Authentication, [SPNEGO](https://en.wikipedia.org/wiki/SPNEGO#:~:text=Simple%20and%20Protected%20GSSAPI%20Negotiation,the%20choice%20of%20security%20technology.&text=This%20can%20help%20organizations%20deploy%20new%20security%20mechanisms%20in%20a%20phased%20manner.)). The proxy works in both forward and reverse modes allowing for authenticating inbound requests via Kerberos, or enriching outgoing requests with Kerberos tickets. **This solution does not depend on on Active Directory joined Windows environment - it uses fully managed Kerberos implementation. It can run on Linux, Windows and does not use MIT Kerberos**

## Ingress

When configured as reverse proxy, the incoming requests are matched to a specific route defined in configuration. If the route is associated with an authorization policy, it will be applied. If the user fails to supply a valid Kerberos ticket in the request, the request will end with a `401 Unauthorized` and the request will not be proxied to the destination. 

The Kerberos ticket verification implementation relies on fully managed Kerberos ticket parser provided via [Kerberos.NET](	<https://github.com/SteveSyfuhs/Kerberos.NET>) library and does not require any communication with the domain controller. **The only requirement to authenticate incoming request and establish identity of the caller from Kerberos ticket is to configure username and password associated with SPN **

### Principal propagation
When the proxy successfully establishes the security principal (identity of the caller + any additional claims) from the Kerberos ticket, it can forward it to the destination service. There following propagation methods are supported:
#### JWT

The proxy forwards the principal as a JWT token to the target application. The JWT token is signed with a configured RSA signing key (PEM format). The calling application can validate the token by retrieving the signing public key that the proxy publishes via standard OpenID Connect discovery mechanism.

The sample payload section of the JWT sent downstream would look as following:

```json
{
  "nbf": 1623776653,
  "exp": 1623776713,
  "iss": "http://localhost:8081",
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/sid": "S-1-5-21-3483396884-3677748265-799010679-1105",
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname": "iwaclient",
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier": "ALMIREX\\iwaclient",
  "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": [
    "Users",
    "MyGroup2",
    "MyGroup"
  ]
}
```



### Configuring routes

Ingress routes are defined with configuration similar to the following:

```
{
  "Proxy": {
    "PrincipalForwardingMode": "Jwt",
    "SigningSecurityKeyId": "JwtSignature",
    "RolesHttpHeaderName": "X-CF-Roles",
    "IdentityHttpHeaderName": "X-CF-Identity",
    "DestinationHeaderName": "X-CF-Forwarded-Url",
    
    "Egress": [
      {
        "ListenPort": 3333,
        "TargetUrl": "http://localhost:8081",
        "CredentialsId": "Caller",
        "Spn": "host/iwasvc.apps.pcfone.io"
      }
    ],
    "Ingress": [
      {
        "ListenPort": "${PORT?8080}",
        "CredentialsId": "ThisApp",
        "Routes": [
          {
            "Id": "JWT secured actuators",
            "Path": "/cloudfoundryapplication**"
          },
          {
            "Id": "WCF WSDLs",
            "Path": "**/*.svc?wsdl"
          },
          {
            "Id": "favicon.ico",
            "Path": "/favicon.ico"
          },
          {
            "Id": "Require Kerberos authentication",
            "Path": "/**",
            "PolicyName": "RequireAuthenticatedUser"
          }
        ]
      }
    ]
  }
  
}

```

.NET applications that use `hwc_buildpack` can leverage [Service Auth Buildpack](https://github.com/macsux/route-service-auth-buildpack) which will install an HTTP Module to automatically extract X-CF-Identity and establish a thread ClaimsPrincipal by the time the user code starts running. When dealing with WCF apps, set upstream calling app to use transport security with client credentials. Disable WCF security in downstream app as the thread identity will already be established by the module injected by the buildpack.



### How to use

Route service requires password of the service principal for which the ticket is intended. This must be supplied either via PRINCIPAL_PASSWORD environmental variable or specifying path to keytab containing the credentials for the principal via KRB5_CLIENT_KTNAME environmental variable
- Push with included manifest
- On the downstream app, create a CUPS with route service URL and bind it to the the application route

### AD Groups

Kerberos tickets contain AD groups the user belongs to, but they are in [SID](https://en.wikipedia.org/wiki/Security_Identifier) format, and are not sent downstream by default. If you want to make assertions on roles, you need to configure LDAP for the route service which will be used to map SIDs to their common names. When configured it will send roles via `X-CF-Roles` in comma delimited format. Groups are loaded once and cached in memory, so no LDAP requests are made per request. The associated middleware will expand full hierarchy of AD groups based on their associations. You can configure LDAP settings either via `appsettings.json` or by setting the following environmental variables:

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

