using Microsoft.AspNetCore.Components;

namespace Samples.TodoApp.UI.Pages;

#if NET9_0_OR_GREATER
[ExcludeFromInteractiveRouting]
#endif
public partial class FusionClose : ComponentBase
{
    [SupplyParameterFromQuery(Name = "flow")]
    public string Flow { get; set; } = "";
}
