using System.Diagnostics.CodeAnalysis;

// From FrenUtil ref.
[assembly: SuppressMessage("Usage", "CA2211:Non-constant fields should not be visible", Justification = "Counter-productive to quality code")]
[assembly: SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Endless false marking of methods whose parameters are defined by delegate/Func/Action usage")]
[assembly: SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "WTF MICROSOFT???")]

// Local
[assembly: SuppressMessage("Interoperability", "SYSLIB1054:Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time")]
[assembly: SuppressMessage("Interoperability", "CA1401:P/Invokes should not be visible")]
[assembly: SuppressMessage("Performance", "CA1806:Do not ignore method results")]
[assembly: SuppressMessage("Performance", "CA1860:Avoid using 'Enumerable.Any()' extension method")]
[assembly: SuppressMessage("Usage", "ASP0018:Unused route parameter")]
[assembly: SuppressMessage("Performance", "CA1861:Avoid constant arrays as arguments")]
