using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ThreeDManager.Web.ModelBinding;

public sealed class FlexibleDecimalModelBinderProvider : IModelBinderProvider
{
    private static readonly IModelBinder Binder = new FlexibleDecimalModelBinder();

    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var modelType = context.Metadata.UnderlyingOrModelType;
        return modelType == typeof(decimal) ? Binder : null;
    }
}
