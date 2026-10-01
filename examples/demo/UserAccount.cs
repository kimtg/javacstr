using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Net.Http;

namespace Com.Example.Demo;
public class UserAccount
{
    private string username;
    private int score;
    private List<string> roles;
    private Dictionary<string, string> metadata;
    public UserAccount(string username, int score)
    {
        this.username = username;
        this.score = score;
        this.roles = new List<string>();
        this.metadata = new Dictionary<string, string>();
    }

    public void AddRole(string role)
    {
        this.roles.Add(role);
    }

    public bool ValidateUsername()
    {
        Regex pattern = new Regex("^[a-zA-Z0-9_]{3,16}$");
        Match matcher = pattern.Match(this.username);
        return matcher.Success;
    }

    public void DisplayInfo()
    {
        Console.WriteLine("User: " + this.username + ", Score: " + this.score);
        foreach (string r in this.roles)
        {
            Console.WriteLine("Role: " + r);
        }
    }

    public string ExtractPrefix()
    {
        if (this.username.Length >= 3)
        {
            return this.username.Substring(0, 3);
        }

        return this.username;
    }
}