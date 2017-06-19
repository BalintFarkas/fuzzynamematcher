# FuzzyNameMatcher
This small library is very useful to fuzzy match person or company names from different databases, from raw chatbot input etc.

Usage:
```csharp
IStringComparerService stringComparerService = new StringComparerService();
var result1 = stringComparerService.GetSimilarityScore("jacko", "Jack Daniels");
var result2 = stringComparerService.GetSimilarityScore("jack", "Jack Daniels");
var result3 = stringComparerService.GetSimilarityScore("john", "Jack Daniels");
```