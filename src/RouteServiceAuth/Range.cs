namespace RouteServiceAuth
{
    public class Range
    {
        public Range(int @from, int to)
        {
            From = @from;
            To = to;
        }

        public int From { get; set; }
        public int To { get; set; }
    }
}