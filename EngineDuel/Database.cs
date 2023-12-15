using System.Collections.Concurrent;
using System.Data;
using Microsoft.Data.Sqlite;

namespace EngineDuel;

public static class Database
{
    public static void GetRandomSample(ConcurrentStack<string> openings, int amount)
    {
        try
        {
            using (SqliteConnection connection = new SqliteConnection("Data Source=openings.db"))
            {
                if (connection.State != ConnectionState.Open)
                    connection.Open();

                using (SqliteCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT moves FROM openings " +
                                      $"WHERE rowid IN (SELECT rowid FROM openings ORDER BY random() LIMIT {amount})";

                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string moves = reader["moves"].ToString();
                            openings.Push(moves);
                        }
                    }
                }

                if (connection.State != ConnectionState.Closed)
                    connection.Close();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }
}