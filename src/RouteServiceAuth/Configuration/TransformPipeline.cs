using System.Collections.Generic;
using RouteServiceAuth.Pipeline;

namespace RouteServiceAuth
{
    public class TransformPipeline
    {
        public string Id { get; set; }
        public List<IRequestTransform> RequestTransforms { get; set; }
        public List<IResponseTransform> ResponseTransforms { get; set; }
    }
}