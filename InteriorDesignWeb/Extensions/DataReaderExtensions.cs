using MySqlConnector;
using System.Data;

namespace InteriorDesignWeb.Extensions
{
    public static class DataReaderExtensions
    {
        public static List<T> ToEntities<T>(this MySqlDataReader reader) where T : new()
        {
            var list = new List<T>();
            var props = typeof(T).GetProperties();

            while (reader.Read())
            {
                var entity = new T();
                foreach (var prop in props)
                {
                    if (HasColumn(reader, prop.Name) && !reader.IsDBNull(prop.Name))
                    {
                        prop.SetValue(entity, reader[prop.Name]);
                    }
                }
                list.Add(entity);
            }
            return list;
        }

        public static async Task<List<T>> ToEntitiesAsync<T>(this MySqlDataReader reader) where T : new()
        {
            var list = new List<T>();
            var props = typeof(T).GetProperties();

            while (await reader.ReadAsync())
            {
                var entity = new T();
                foreach (var prop in props)
                {
                    if (HasColumn(reader, prop.Name) && !reader.IsDBNull(prop.Name))
                    {
                        prop.SetValue(entity, reader[prop.Name]);
                    }
                }
                list.Add(entity);
            }
            return list;
        }

        private static bool HasColumn(MySqlDataReader reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
