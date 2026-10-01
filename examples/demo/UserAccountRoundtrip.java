package com.example.demo;

import java.util.*;
import java.util.regex.*;
import java.net.http.*;
import java.net.URI;

public class UserAccount
{
    private String username;

    private int score;

    private List<String> roles;

    private Map<String, String> metadata;

    public UserAccount(String username, int score)
    {
        this.username = username;
        this.score = score;
        this.roles = new ArrayList<String>();
        this.metadata = new HashMap<String, String>();
    }

    public void addRole(String role)
    {
        this.roles.add(role);
    }

    public boolean validateUsername()
    {
        Pattern pattern = Pattern.compile("^[a-zA-Z0-9_]{3,16}$");
        Matcher matcher = pattern.matcher(this.username);
        return matcher.find();
    }

    public void displayInfo()
    {
        System.out.println("User: " + this.username + ", Score: " + this.score);
        for (String r : this.roles)
        {
            System.out.println("Role: " + r);
        }
    }

    public String extractPrefix()
    {
        if (this.username.length() >= 3)
        {
            return this.username.substring(0, 3);
        }
        return this.username;
    }
}
