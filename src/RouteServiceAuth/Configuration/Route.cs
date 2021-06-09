namespace RouteServiceAuth
{
    public class Route
    {
        private string _id;

        public string Id
        {
            get => _id ?? $"[Path={Path},PolicyName={PolicyName}]";
            set => _id = value;
        }

        public string Path { get; set; }
        public string PolicyName { get; set; }
    }
}