namespace LocalProjections
{
    using System;
    using System.Globalization;

    public struct AllStreamPosition : IEquatable<AllStreamPosition>, IComparable<AllStreamPosition>
    {
        private readonly long? _value;

        /// <summary>
        /// The fact we don't yet have an $all stream position.
        /// </summary>
        public static readonly AllStreamPosition None = new AllStreamPosition();

        /// <summary>
        /// Returns the maximum of the <paramref name="left"/> and <paramref name="right"/> positions.
        /// </summary>
        /// <param name="left">The left position.</param>
        /// <param name="right">The right position.</param>
        /// <returns>The maximum of these two positions.</returns>
        public static AllStreamPosition Max(AllStreamPosition left, AllStreamPosition right)
        {
            var comparison = left.CompareTo(right);
            return comparison > 0 ? left : right;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AllStreamPosition"/>.
        /// </summary>
        /// <param name="value">The positive value of the position in the $all stream.</param>
        /// <exception cref="ArgumentOutOfRangeException">The value of the all stream position must be 0 or greater.</exception>
        public AllStreamPosition(long value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "The value of the all stream position must be 0 or greater.");
            _value = value;
        }

        public bool Equals(AllStreamPosition other) =>
            _value == other._value;

        public override string ToString() =>
            _value?.ToString(CultureInfo.InvariantCulture) ?? "<null>";

        public int CompareTo(AllStreamPosition other)
        {
            if (!_value.HasValue && !other._value.HasValue) return 0;
            if (!_value.HasValue) return -1;
            if (!other._value.HasValue) return 1;
            return _value.Value.CompareTo(other._value.Value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is AllStreamPosition && Equals((AllStreamPosition)obj);
        }

        public override int GetHashCode() =>
            _value.GetHashCode();

        public static bool operator ==(AllStreamPosition left, AllStreamPosition right) =>
            left.Equals(right);

        public static bool operator !=(AllStreamPosition left, AllStreamPosition right) =>
            !left.Equals(right);

        public static bool operator <(AllStreamPosition left, AllStreamPosition right) =>
            left.CompareTo(right) == -1;

        public static bool operator <=(AllStreamPosition left, AllStreamPosition right) =>
            left.CompareTo(right) <= 0;

        public static bool operator >(AllStreamPosition left, AllStreamPosition right) =>
            left.CompareTo(right) == 1;

        public static bool operator >=(AllStreamPosition left, AllStreamPosition right) =>
            left.CompareTo(right) >= 0;

        public static implicit operator long? (AllStreamPosition instance) =>
            instance._value;

        public long? ToNullableInt64() =>
            _value;

        public long ToInt64() =>
            _value ?? -1;

        public AllStreamPosition Shift(int offset = 1) =>
            new AllStreamPosition(ToInt64() + offset);

        public static AllStreamPosition FromNullableInt64(long? position) =>
            position.HasValue ? new AllStreamPosition(position.Value) : None;
    }
}
