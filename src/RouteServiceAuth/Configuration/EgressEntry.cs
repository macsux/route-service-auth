// using FluentValidation;
//
// namespace RouteServiceAuth
// {
//     public class EgressEntry : ProxyEntry
//     {
//         private class Validator : AbstractValidator<EgressEntry>
//         {
//             public Validator()
//             {
//                 RuleFor(x => x.Password).NotEmpty();
//                 RuleFor(x => x.ListenPort).NotEmpty().GreaterThan(0);
//                 RuleFor(x => x.TargetUrl).NotEmpty();
//                 RuleFor(x => x.UserAccount).NotEmpty();
//             }
//         }
//     }
// }