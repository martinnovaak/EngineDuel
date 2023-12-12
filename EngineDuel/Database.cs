using System.Collections.Concurrent;
using System.Data;
using Microsoft.Data.Sqlite;

namespace EngineDuel;

public class Database
{
    public SqliteConnection connection { get; private set; }
    
    public Database()
    {
        connection = new SqliteConnection("Data Source=openings.db");
    }

    public void OpenConnection()
    {
        if (connection.State != ConnectionState.Open)
            connection.Open();
    }

    public void CloseConnection()
    {
        if (connection.State != ConnectionState.Closed)
            connection.Close();
    }

    public ConcurrentStack<string> GetRandomSample()
    {
        ConcurrentStack<string> results = new ();
        
        connection.Open();

        using (SqliteCommand cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT moves FROM openings " +
                              "WHERE rowid IN (SELECT rowid FROM openings ORDER BY random() LIMIT 100)";

            using (SqliteDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string moves = reader["moves"].ToString();
                    results.Push(moves);
                }
            }
        }

        connection.Close();

        return results;
    }
}