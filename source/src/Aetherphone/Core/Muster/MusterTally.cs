using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Muster;

internal readonly record struct MusterTally(int OnTheWay, int Late, int Here, int Asking)
{
    public int Total => OnTheWay + Late + Here + Asking;

    public static MusterTally Of(MusterAttendeeDto[] attendees)
    {
        var onTheWay = 0;
        var late = 0;
        var here = 0;
        var asking = 0;
        for (var index = 0; index < attendees.Length; index++)
        {
            switch (attendees[index].Status)
            {
                case MusterStatuses.RunningLate:
                    late++;
                    break;
                case MusterStatuses.Here:
                    here++;
                    break;
                case MusterStatuses.WhereExactly:
                    asking++;
                    break;
                default:
                    onTheWay++;
                    break;
            }
        }

        return new MusterTally(onTheWay, late, here, asking);
    }
}
