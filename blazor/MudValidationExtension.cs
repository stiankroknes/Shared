// Validate related properties
public static class Validation
{
    public static Func<object, string, Task<IEnumerable<string>>> ValidateValue<T>(AbstractValidator<T> validator) =>
        async (model, propertyName) =>
        {
            var result =
                await validator.ValidateAsync(
                    ValidationContext<T>.CreateWithOptions((T)model, x => x.IncludeProperties(propertyName)));

            return result.IsValid
                ? Array.Empty<string>()
                : result.Errors.Select(e => e.ErrorMessage);
        };

    public static Func<object, string, Task<IEnumerable<string>>> ValidateValueAndDependent<T>(AbstractValidator<T> validator,
        IReadOnlyCollection<IFormComponent> formControls)
    {
        return async (model, propertyName) =>
        {
            var relatedProperty = GetPropertyNamesForDependentRules(validator, propertyName);
            var additionalControlsToValidate = GetFormComponentsByPropertyNames(relatedProperty, formControls);

            foreach (var control in additionalControlsToValidate)
            {
                await control.Validate();
            }

            var result =
                await validator.ValidateAsync(
                    ValidationContext<T>.CreateWithOptions((T)model, x => x.IncludeProperties(propertyName)));

            return result.IsValid
                ? Array.Empty<string>()
                : result.Errors
                    .Select(e => (e.ErrorMessage));
        };
    }

    private static IEnumerable<string> GetPropertyNamesForDependentRules<T>(AbstractValidator<T> validator, string propertyName)
    {
        var relatedProperty = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rule in validator)
        {
            foreach (var component in rule.Components)
            {
                if (component.Validator is IComparisonValidator comparison &&
                    string.Equals(comparison.MemberToCompare?.Name, propertyName, StringComparison.Ordinal))
                {
                    // Comparison validators where MemberToCompare matches the current validation propertyName.
                    // Collect the property name for this rule.
                    relatedProperty.Add(rule.PropertyName);
                }
            }
        }

        return relatedProperty;
    }

    private static IEnumerable<IFormComponent> GetFormComponentsByPropertyNames(IEnumerable<string> relatedProperty, IReadOnlyCollection<IFormComponent> formControls)
    {
        var controlsToValidate = new List<IFormComponent>();

        foreach (var property in relatedProperty)
        {
            controlsToValidate.AddRange(formControls
                .Where(control =>
                    string.Equals(property, control.GetPropertyNameByForExpression(), StringComparison.Ordinal)));
        }

        return controlsToValidate;
    }

    private static string GetPropertyNameByForExpression(this IFormComponent formComponent)
    {
        if (formComponent.IsForNull)
        {
            return string.Empty;
        }
        
        // NOTE: We could cache this.
        var forExpression = formComponent.GetType().GetProperty("For")?.GetValue(formComponent);
        return TryGetPath(forExpression);

        static string TryGetPath(dynamic? forExpression)
        {
            // Consider other alternatives here ? For is of type Expression<Func<T>>?
            if (forExpression is null)
            {
                return string.Empty;
            }

            try
            {
                return ExpressionExtensions.GetFullPathOfMember(forExpression);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
