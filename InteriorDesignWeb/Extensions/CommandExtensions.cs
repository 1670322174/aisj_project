using MySqlConnector;

namespace InteriorDesignWeb.Extensions
{
    public static class CommandExtensions
    {
        public static void AddParameters(this MySqlCommand cmd, object? parameters)
        {
            if (parameters == null) return;

            foreach (var prop in parameters.GetType().GetProperties())
            {
                var paramName = $"@{prop.Name}";
                var value = prop.GetValue(parameters) ?? DBNull.Value;

                if (!cmd.Parameters.Contains(paramName))
                {
                    cmd.Parameters.AddWithValue(paramName, value);
                }
            }
        }
    }
}
