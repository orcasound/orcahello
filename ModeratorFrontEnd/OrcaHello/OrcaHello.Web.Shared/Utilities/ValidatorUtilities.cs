namespace OrcaHello.Web.Shared.Utilities
{
    [ExcludeFromCodeCoverage]
    public static class ValidatorUtilities
    {
        public static bool IsInvalid(string input) => string.IsNullOrWhiteSpace(input);
        public static bool IsInvalid(long input) => input == default(long);
        public static bool IsInvalid(Guid input) => input == Guid.Empty;
        public static bool IsInvalid<TValue>(this TValue value) where TValue : Enum => !Enum.IsDefined(typeof(TValue), value);
        public static bool IsNegative(long input) => input < 0;
        public static bool IsNegative(int input) => input < 0;
        public static bool IsZeroOrLess(int input) => input <= 0;
        public static bool IsInvalid(object input) => input == null;
        public static bool IsInvalid(DateTime input) => input == default(DateTime);
        public static bool IsInvalidGuidString(string input) => !Guid.TryParse(input, out Guid dummy);
        public static string GetInnerMessage(Exception exception) => exception.InnerException.Message;
        public static string GetMessage(Exception exception) => exception.Message;

        public static string GetMatchingEnumValue(string input, Type enumType)
        {
            if (Enum.TryParse(enumType, input, true, out object enumValue))
            {
                return enumValue.ToString();
            }

            return null;
        }
    }
}
