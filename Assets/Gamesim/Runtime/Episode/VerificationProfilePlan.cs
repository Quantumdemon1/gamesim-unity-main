using System;

namespace Gamesim.Episode
{
    public enum VerificationProfileAction { Room, Journal, Settings, Station }

    public readonly struct VerificationProfileStep
    {
        public readonly VerificationProfileAction Action;
        public readonly int RoomIndex;
        internal VerificationProfileStep(VerificationProfileAction action, int roomIndex = -1)
        { Action = action; RoomIndex = roomIndex; }
    }

    /// <summary>QA-only schedule: every room, then each UI/station action, regardless of room count.</summary>
    public static class VerificationProfilePlan
    {
        public static VerificationProfileStep Step(int actionNumber, int roomCount)
        {
            if (actionNumber < 0) throw new ArgumentOutOfRangeException(nameof(actionNumber));
            if (roomCount < 0 || roomCount > int.MaxValue - 3)
                throw new ArgumentOutOfRangeException(nameof(roomCount));
            int step = actionNumber % (roomCount + 3);
            if (step < roomCount) return new VerificationProfileStep(VerificationProfileAction.Room, step);
            switch (step - roomCount)
            {
                case 0: return new VerificationProfileStep(VerificationProfileAction.Journal);
                case 1: return new VerificationProfileStep(VerificationProfileAction.Settings);
                default: return new VerificationProfileStep(VerificationProfileAction.Station);
            }
        }
    }
}
