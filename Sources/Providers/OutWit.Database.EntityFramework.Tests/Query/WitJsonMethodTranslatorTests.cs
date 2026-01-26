using System.Reflection;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using OutWit.Database.EntityFramework.Query.Translators;

namespace OutWit.Database.EntityFramework.Tests.Query;

/// <summary>
/// Unit tests for <see cref="WitJsonMethodTranslator"/>.
/// </summary>
[TestFixture]
public class WitJsonMethodTranslatorTests
{
    #region JsonValue Tests

    [Test]
    public void JsonValueMethodExistsTest()
    {
        var method = typeof(WitJsonExtensions).GetMethod(
            nameof(WitJsonExtensions.JsonValue),
            new[] { typeof(string), typeof(string) });

        Assert.That(method, Is.Not.Null);
    }

    [Test]
    public void JsonValueThrowsWhenCalledDirectlyTest()
    {
        var json = @"{""name"": ""test""}";

        Assert.Throws<InvalidOperationException>(() => json.JsonValue("$.name"));
    }

    #endregion

    #region JsonQuery Tests

    [Test]
    public void JsonQueryMethodExistsTest()
    {
        var method = typeof(WitJsonExtensions).GetMethod(
            nameof(WitJsonExtensions.JsonQuery),
            new[] { typeof(string), typeof(string) });

        Assert.That(method, Is.Not.Null);
    }

    [Test]
    public void JsonQueryThrowsWhenCalledDirectlyTest()
    {
        var json = @"{""items"": [1, 2, 3]}";

        Assert.Throws<InvalidOperationException>(() => json.JsonQuery("$.items"));
    }

    #endregion

    #region JsonContains Tests

    [Test]
    public void JsonContainsMethodExistsTest()
    {
        var method = typeof(WitJsonExtensions).GetMethod(
            nameof(WitJsonExtensions.JsonContains),
            new[] { typeof(string), typeof(string) });

        Assert.That(method, Is.Not.Null);
    }

    [Test]
    public void JsonContainsThrowsWhenCalledDirectlyTest()
    {
        var json = @"{""name"": ""test""}";

        Assert.Throws<InvalidOperationException>(() => json.JsonContains("test"));
    }

    #endregion

    #region JsonLength Tests

    [Test]
    public void JsonLengthMethodExistsTest()
    {
        var method = typeof(WitJsonExtensions).GetMethod(
            nameof(WitJsonExtensions.JsonLength),
            new[] { typeof(string) });

        Assert.That(method, Is.Not.Null);
    }

    [Test]
    public void JsonLengthThrowsWhenCalledDirectlyTest()
    {
        var json = @"[1, 2, 3]";

        Assert.Throws<InvalidOperationException>(() => json.JsonLength());
    }

    #endregion

    #region JsonType Tests

    [Test]
    public void JsonTypeMethodExistsTest()
    {
        var method = typeof(WitJsonExtensions).GetMethod(
            nameof(WitJsonExtensions.JsonType),
            new[] { typeof(string) });

        Assert.That(method, Is.Not.Null);
    }

    [Test]
    public void JsonTypeThrowsWhenCalledDirectlyTest()
    {
        var json = @"{""name"": ""test""}";

        Assert.Throws<InvalidOperationException>(() => json.JsonType());
    }

    #endregion

    #region JsonValid Tests

    [Test]
    public void JsonValidMethodExistsTest()
    {
        var method = typeof(WitJsonExtensions).GetMethod(
            nameof(WitJsonExtensions.JsonValid),
            new[] { typeof(string) });

        Assert.That(method, Is.Not.Null);
    }

    [Test]
    public void JsonValidThrowsWhenCalledDirectlyTest()
    {
        var json = @"{""name"": ""test""}";

        Assert.Throws<InvalidOperationException>(() => json.JsonValid());
    }

    #endregion

    #region Extension Methods Signature Tests

    [Test]
    public void AllJsonExtensionMethodsAreStaticTest()
    {
        var methods = typeof(WitJsonExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static);
        var jsonMethods = methods.Where(m => m.Name.StartsWith("Json")).ToList();

        Assert.That(jsonMethods.Count, Is.GreaterThanOrEqualTo(6));
        Assert.That(jsonMethods.All(m => m.IsStatic), Is.True);
    }

    [Test]
    public void AllJsonExtensionMethodsHaveThisParameterTest()
    {
        var methods = typeof(WitJsonExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static);
        var jsonMethods = methods.Where(m => m.Name.StartsWith("Json")).ToList();

        foreach (var method in jsonMethods)
        {
            var parameters = method.GetParameters();
            Assert.That(parameters.Length, Is.GreaterThanOrEqualTo(1));
            Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(string)));
        }
    }

    #endregion
}
