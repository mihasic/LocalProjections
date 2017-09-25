namespace LocalProjections
{
    public class Envelope
    {
        public readonly AllStreamPosition Checkpoint;
        public readonly object Payload;

        public Envelope(AllStreamPosition checkpoint, object payload)
        {
            Checkpoint = checkpoint;
            Payload = payload;
        }
    }
}