namespace Exercise.Infra.Exceptions
{
    public static class ExceptionExtensions
    {
        public static object? GetParameters(this Exception exception)
        {
            var parameterizedException = exception
            .GetType()
            .GetInterfaces()
            .FirstOrDefault(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IExceptionWithParameters<>));

            if (parameterizedException is null)
                return null;

            var parametersProperty = parameterizedException.GetProperty(nameof(IExceptionWithParameters<object>.Parameters));
            var parameters = parametersProperty?.GetValue(exception);

            return parameters;
        }
    }
}
