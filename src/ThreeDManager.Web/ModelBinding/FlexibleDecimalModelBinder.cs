using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ThreeDManager.Web.ModelBinding;

public sealed class FlexibleDecimalModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueProviderResult == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueProviderResult);

        var value = valueProviderResult.FirstValue;
        if (string.IsNullOrWhiteSpace(value))
        {
            bindingContext.Result = ModelBindingResult.Success(null);
            return Task.CompletedTask;
        }

        if (TryParseDecimal(value, valueProviderResult.Culture, out var parsedValue))
        {
            bindingContext.Result = ModelBindingResult.Success(parsedValue);
            return Task.CompletedTask;
        }

        bindingContext.ModelState.TryAddModelError(
            bindingContext.ModelName,
            $"O valor '{value}' não é um número decimal válido.");

        return Task.CompletedTask;
    }

    private static bool TryParseDecimal(string value, CultureInfo culture, out decimal parsedValue)
    {
        var trimmedValue = value.Trim();

        if (trimmedValue.Contains('.') && !trimmedValue.Contains(','))
        {
            return decimal.TryParse(
                trimmedValue,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out parsedValue);
        }

        if (decimal.TryParse(trimmedValue, NumberStyles.Number, culture, out parsedValue))
        {
            return true;
        }

        var normalizedValue = trimmedValue.Replace(',', '.');
        return decimal.TryParse(
            normalizedValue,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out parsedValue);
    }
}
