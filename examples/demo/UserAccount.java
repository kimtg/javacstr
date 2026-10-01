package com.example.demo;

import java.util.List;
import java.util.ArrayList;
import java.util.Map;
import java.util.HashMap;
import java.util.regex.Pattern;
import java.util.regex.Matcher;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.net.URI;

public class UserAccount {
    private String username;
    private int score;
    private List<String> roles;
    private Map<String, String> metadata;

    public UserAccount(String username, int score) {
        this.username = username;
        this.score = score;
        this.roles = new ArrayList<>();
        this.metadata = new HashMap<>();
    }

    public void addRole(String role) {
        this.roles.add(role);
    }

    public boolean validateUsername() {
        Pattern pattern = Pattern.compile("^[a-zA-Z0-9_]{3,16}$");
        Matcher matcher = pattern.matcher(this.username);
        return matcher.find();
    }

    public void displayInfo() {
        System.out.println("User: " + this.username + ", Score: " + this.score);
        for (String r : this.roles) {
            System.out.println("Role: " + r);
        }
    }

    public String extractPrefix() {
        if (this.username.length() >= 3) {
            return this.username.substring(0, 3);
        }
        return this.username;
    }
}
