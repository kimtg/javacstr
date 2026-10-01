using Xunit;
using Javacstr.Core.Models;
using Javacstr.Core.Translation;

namespace Javacstr.Tests;

public class HttpsTranslationTests
{
    private readonly CodeTranslator _translator = new(new TranslationOptions
    {
        MapHttpClient = true,
        UseFileScopedNamespaces = true,
        ConvertMethodCasing = true
    });

    [Fact]
    public void TestJavaHttpsToCSharp()
    {
        string java = """
        package com.example.http;

        import java.net.http.HttpClient;
        import java.net.http.HttpRequest;
        import java.net.http.HttpResponse;
        import java.net.URI;

        public class WebFetcher {
            public void fetchSecureData() {
                HttpClient client = HttpClient.newHttpClient();
                HttpRequest req = HttpRequest.newBuilder().uri(URI.create("https://api.example.com/data")).build();
                HttpResponse response = client.send(req, HttpResponse.BodyHandlers.ofString());
                int code = response.statusCode();
                String body = response.body();
            }
        }
        """;

        var result = _translator.TranslateJavaToCSharp(java);
        Assert.True(result.Success, string.Join("\n", result.Errors));

        string cs = result.OutputCode;
        Assert.Contains("using System.Net.Http;", cs);
        Assert.Contains("HttpClient client = new HttpClient();", cs);
        Assert.Contains("HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, \"https://api.example.com/data\");", cs);
        Assert.Contains("HttpResponseMessage response = client.Send(req);", cs);
        Assert.Contains("int code = (int)response.StatusCode;", cs);
        Assert.Contains("string body = response.Content.ReadAsStringAsync().Result;", cs);
    }

    [Fact]
    public void TestCSharpHttpsToJava()
    {
        string csharp = """
        namespace Com.Example.Http;

        using System.Net.Http;

        public class WebFetcher
        {
            public void FetchSecureData()
            {
                HttpClient client = new HttpClient();
                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/data");
                HttpResponseMessage response = client.Send(req);
                int code = (int)response.StatusCode;
                string body = response.Content.ReadAsStringAsync().Result;
            }
        }
        """;

        var result = _translator.TranslateCSharpToJava(csharp);
        Assert.True(result.Success, string.Join("\n", result.Errors));

        string java = result.OutputCode;
        Assert.Contains("import java.net.http.*;", java);
        Assert.Contains("HttpClient client = HttpClient.newHttpClient();", java);
        Assert.Contains("HttpRequest req = HttpRequest.newBuilder().uri(URI.create(\"https://api.example.com/data\")).build();", java);
        Assert.Contains("HttpResponse<String> response = client.send(req, HttpResponse.BodyHandlers.ofString());", java);
        Assert.Contains("int code = response.statusCode();", java);
        Assert.Contains("String body = response.body();", java);
    }
}
