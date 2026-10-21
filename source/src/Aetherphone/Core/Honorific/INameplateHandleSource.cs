namespace Aetherphone.Core.Honorific;

internal interface INameplateHandleSource
{
    NameplateStatus TagStatus { get; }
    string ResolveNameplateHandle();
}
