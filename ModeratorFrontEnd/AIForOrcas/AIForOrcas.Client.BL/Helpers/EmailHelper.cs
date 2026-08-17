namespace AIForOrcas.Client.BL.Helpers
{
    public static class EmailHelper
    {
        public static string ExtractName(string email)
        {
            if (string.IsNullOrEmpty(email) ||
                (!email.Contains("@") && !email.Contains("#")))
                return email;

            var working = email;
            if (working.Contains("@"))
                working = working.Split('@')[0];

            if (working.Contains("#"))
                working = working.Split('#')[1];

            return working;
        }
    }
}
