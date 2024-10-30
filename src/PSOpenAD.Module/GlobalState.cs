using System;
using System.Collections.Generic;

namespace PSOpenAD.Module;

internal static class GlobalState
{
    /// <summary>Register the session into the global state.</summary>
    public static void RegisterSession(OpenADSession session)
    {
        Sessions.Add(session);
        session.Connection.Session.StateChanged += (_, state) =>
        {
            if (state == LDAP.SessionState.Closed)
            {
                Sessions.Remove(session);
            }
        };
        SchemaMetadata = session.SchemaMetadata;
    }

    /// <summary>List of sessions that have been opened by the client.</summary>
    public static List<OpenADSession> Sessions = new();

    /// <summary>Information about LDAP classes and their attributes.</summary>
    public static SchemaMetadata? SchemaMetadata;

    /// <summary>The default domain controller hostname to use when none was provided.</summary>
    public static Uri? DefaultDC;

    /// <summary>If the default DC couldn't be detected this stores the details.</summary>
    public static string? DefaultDCError;
}