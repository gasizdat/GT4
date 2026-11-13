namespace GT4.UI.Abstraction;

public class ModelDownloadException(string message, Exception innerException) : Exception(message, innerException);
