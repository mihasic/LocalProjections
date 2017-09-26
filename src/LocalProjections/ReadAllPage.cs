namespace LocalProjections
{
    using System.Collections.Generic;

    public class ReadAllPage
    {
        public readonly AllStreamPosition FromPosition;
        public readonly AllStreamPosition NextPosition;
        public readonly bool IsEnd;
        public readonly IReadOnlyCollection<Envelope> Messages;

        public ReadAllPage(
            AllStreamPosition fromPosition,
            AllStreamPosition nextPosition,
            bool isEnd,
            IReadOnlyCollection<Envelope> messages
        )
        {
            FromPosition = fromPosition;
            NextPosition = nextPosition;
            IsEnd = isEnd;
            Messages = messages;
        }
    }
}