using System;
using System.Collections.Generic;

namespace PSOpenAD;

internal static class GlobalState
{
    /// <summary>List of sessions that have been opened by the client.</summary>
    public static List<OpenADSession> Sessions = new();

    /// <summary>Keeps the current session count used to uniquely identify each new session.</summary>
    public static int SessionCounter = 1;

    /// <summary>Information about LDAP classes and their attributes.</summary>
    public static SchemaMetadata? SchemaMetadata;

    /// <summary>The default domain controller hostname to use when none was provided.</summary>
    public static Uri? DefaultDC;

    /// <summary>If the default DC couldn't be detected this stores the details.</summary>
    public static string? DefaultDCError;
}