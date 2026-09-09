using System;
using Newtonsoft.Json.Linq;

namespace LesserDashboardClient.Helpers;

/// <summary>
/// Leitura local do loginToken como JWT — só estrutura + claim <c>exp</c>, sem validar assinatura
/// (o servidor continua sendo a autoridade real). Serve para dois usos no cliente:
///   1) no startup, decidir se vale tentar usar o token guardado ou já pedir login;
///   2) mostrar a validade da sessão para o usuário no topo do app.
/// </summary>
public static class JwtSessionInfo
{
    /// <summary>Tolerância de relógio ao comparar <c>exp</c> com "agora".</summary>
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(2);

    /// <summary>
    /// true se <paramref name="token"/> tem forma de JWT (3 segmentos base64url, payload JSON
    /// legível) E tem um claim <c>exp</c> numérico ainda no futuro (com tolerância de relógio).
    /// Qualquer outra coisa (null, token legado opaco, JWT malformado, <c>exp</c> no passado) → false.
    /// </summary>
    public static bool IsValidJwt(string? token)
    {
        var exp = GetExpiration(token);
        return exp != null && exp.Value > DateTimeOffset.UtcNow - ClockSkew;
    }

    /// <summary>
    /// Instante de expiração (UTC) lido do claim <c>exp</c> do payload do JWT, ou null se o token
    /// não for um JWT legível ou não tiver <c>exp</c> numérico.
    /// </summary>
    public static DateTimeOffset? GetExpiration(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var parts = token.Split('.');
        if (parts.Length != 3)
            return null;

        try
        {
            var payloadJson = System.Text.Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            var payload = JObject.Parse(payloadJson);
            var expToken = payload["exp"];
            if (expToken == null || (expToken.Type != JTokenType.Integer && expToken.Type != JTokenType.Float))
                return null;
            return DateTimeOffset.FromUnixTimeSeconds((long)expToken);
        }
        catch
        {
            return null;
        }
    }

    private static byte[] Base64UrlDecode(string input)
    {
        string s = input.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }
}
