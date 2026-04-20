using System;
using Microsoft.Data.SqlClient;

class Program
{
    static void Main()
    {
        string connStr = "Server=NEYMAR\\MKT;Database=TanHuyComputer;Trusted_Connection=True;TrustServerCertificate=True;";
        using var conn = new SqlConnection(connStr);
        conn.Open();
        try {
            using var cmd = new SqlCommand("SELECT category_name, slug FROM Categories", conn);
            using var reader = cmd.ExecuteReader();
            while(reader.Read()) {
                Console.WriteLine($"{reader["category_name"]} -> {reader["slug"]}");
            }
        } catch(Exception e) {
            Console.WriteLine(e.Message);
        }
    }
}
