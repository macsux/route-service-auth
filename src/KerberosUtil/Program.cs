using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using Kerberos.NET.Client;
using Kerberos.NET.Credentials;

namespace KerberosUtil
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            var getTicketCommand = new Command("get-ticket")
            {
                new Option<string>(
                    "--kdc",
                    description: "Key Destribution Center (KDC). Try AD domain name if you don't know this value - will usually work") {Required = true},
                new Option<string>(
                    "--user",
                    description: "Kerberos client principal. Ex. someuser@domain.com") {Required = true},
                new Option<string>(
                    "--password",
                    "Password for the client principal") {Required = true},
                new Option<string>(
                    "--spn",
                    "Destination service account or SPN. Ex http/myservice.domain.com") {Required = true}
            };
            getTicketCommand.Handler = CommandHandler.Create<string,string,string,string>(async(kdc, user, password, spn) =>
            {
                var client = new KerberosClient(kdc);

                var kerbCred = new KerberosPasswordCredential(user, password);
                await client.Authenticate(kerbCred);

                var ticket = await client.GetServiceTicket(spn);
                var ticket64 = Convert.ToBase64String(ticket.EncodeApplication().ToArray());
                Console.WriteLine(ticket64);
                
            });
            var rootCommand = new RootCommand();
            rootCommand.AddCommand(getTicketCommand);
            return await rootCommand.InvokeAsync(args);
        }
    }
}