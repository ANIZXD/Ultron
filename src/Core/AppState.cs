using System;

namespace Ultron.Core
{
    public enum UltronState
    {
        Idle,
        Listening,
        Thinking,
        Speaking,
        Muted,
        Error,
        TakenOver
    }

    public enum PermissionState
    {
        Locked,
        Pending,
        Approved,
        Revoked
    }

    public static class StateText
    {
        public static string ForState(UltronState state)
        {
            switch (state)
            {
                case UltronState.Listening:
                    return "LISTENING";
                case UltronState.Thinking:
                    return "THINKING";
                case UltronState.Speaking:
                    return "SPEAKING";
                case UltronState.Muted:
                    return "MUTED";
                case UltronState.Error:
                    return "ERROR";
                case UltronState.TakenOver:
                    return "TAKEN OVER";
                default:
                    return "IDLE";
            }
        }

        public static string ForPermission(PermissionState state)
        {
            switch (state)
            {
                case PermissionState.Pending:
                    return "Awaiting approval";
                case PermissionState.Approved:
                    return "Approved";
                case PermissionState.Revoked:
                    return "Revoked";
                default:
                    return "Locked";
            }
        }
    }
}
