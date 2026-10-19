namespace MyMIS.Api.Authorization;

// A "marker" attribute: it holds no data and runs no code.
// It only labels an action as "still reachable while a password change is pending".
// The middleware in Step 4 looks for this label on the matched endpoint.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class AllowWhenPasswordChangeRequiredAttribute : Attribute
{
}