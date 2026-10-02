using Npgsql;
using NpgsqlTypes;

namespace Altinn.Auth.AuditLog.Persistence.Extensions;

/// <summary>
/// Extension methods for Npgsql.
/// </summary>
internal static class NpgsqlExtensions
{
    /// <summary>
    /// Adds a typed <see cref="NpgsqlParameter{T}"/> to the <see cref="NpgsqlParameterCollection"/> given the specified
    /// parameter name and data type. Set the value through <see cref="NpgsqlParameter{T}.TypedValue"/>.
    /// </summary>
    /// <typeparam name="T">The CLR type of the parameter value.</typeparam>
    /// <param name="parameters">The <see cref="NpgsqlParameterCollection"/> to add the parameter to.</param>
    /// <param name="parameterName">The name of the parameter.</param>
    /// <param name="parameterType">One of the <see cref="NpgsqlDbType"/> values.</param>
    /// <returns>The parameter that was added.</returns>
    public static NpgsqlParameter<T> Add<T>(
        this NpgsqlParameterCollection parameters,
        string parameterName,
        NpgsqlDbType parameterType)
    {
        var parameter = new NpgsqlParameter<T>(parameterName, parameterType);
        parameters.Add(parameter);
        return parameter;
    }
}
