namespace MAK3R.Core;

public static class Errors
{
    public static class General
    {
        public const string UnexpectedError = "An unexpected error occurred";
        public const string ValidationFailed = "Validation failed";
        public const string NotFound = "Resource not found";
        public const string AccessDenied = "Access denied";
        public const string InvalidOperation = "Invalid operation";
    }

    public static class DigitalTwin
    {
        public const string CompanyNotFound = "Company not found";
        public const string SiteNotFound = "Site not found";
        public const string MachineNotFound = "Machine not found";
        public const string ProductNotFound = "Product not found";
        public const string InvalidTwinStructure = "Invalid digital twin structure";
        public const string OrchestrationFailed = "Twin orchestration failed";
    }

    public static class Connector
    {
        public const string ConnectionFailed = "Failed to connect to external system";
        public const string AuthenticationFailed = "Authentication failed";
        public const string InvalidConfiguration = "Invalid connector configuration";
        public const string DataMappingFailed = "Data mapping failed";
        public const string SyncFailed = "Data synchronization failed";
    }

    public static class Anomaly
    {
        public const string RuleNotFound = "Anomaly rule not found";
        public const string InvalidRule = "Invalid anomaly rule";
        public const string DetectionFailed = "Anomaly detection failed";
        public const string RuleEngineError = "Rule engine error";
    }

    public static class Identity
    {
        public const string UserNotFound = "User not found";
        public const string InvalidCredentials = "Invalid credentials";
        public const string TokenExpired = "Token has expired";
        public const string InsufficientPermissions = "Insufficient permissions";
    }
}