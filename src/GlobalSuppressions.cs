// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

// From FrenUtil ref.
[assembly: SuppressMessage("Usage", "CA2211:Non-constant fields should not be visible", Justification = "Counter-productive to quality code")]
[assembly: SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Endless false marking of methods whose parameters are defined by delegate/Func/Action usage")]
[assembly: SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "WTF MICROSOFT???")]
