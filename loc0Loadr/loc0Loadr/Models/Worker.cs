namespace loc0Loadr.Models
{
    internal class Worker
    {
        public long StartingOffset { get; set; }
        public long EndOffset { get; set; }
        public long ByteRange => EndOffset - StartingOffset;
        public int OrderId { get; set; }
    }
}