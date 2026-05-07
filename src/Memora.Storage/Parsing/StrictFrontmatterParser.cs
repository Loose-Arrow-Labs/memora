using Memora.Core.Validation;

namespace Memora.Storage.Parsing;

internal static class StrictFrontmatterParser
{
    public static FrontmatterOnlyParseResult Parse(string frontmatter)
    {
        var parser = new Parser(frontmatter);
        return parser.Parse();
    }

    internal sealed record FrontmatterOnlyParseResult(
        IReadOnlyDictionary<string, object?> Frontmatter,
        ArtifactValidationResult Validation);

    private sealed class Parser
    {
        private const int MaxDepth = 32;

        private readonly string[] _lines;
        private readonly List<ArtifactValidationIssue> _issues = [];
        private int _index;

        public Parser(string frontmatter)
        {
            _lines = frontmatter.Split('\n');
        }

        public FrontmatterOnlyParseResult Parse()
        {
            var frontmatter = ParseMapping(0, depth: 0);

            if (_issues.Count > 0)
            {
                return new FrontmatterOnlyParseResult(new Dictionary<string, object?>(), new ArtifactValidationResult(_issues));
            }

            return new FrontmatterOnlyParseResult(frontmatter, ArtifactValidationResult.Success);
        }

        private Dictionary<string, object?> ParseMapping(int indent, int depth)
        {
            var values = new Dictionary<string, object?>(StringComparer.Ordinal);

            if (DepthExceeded(depth))
            {
                return values;
            }

            while (_index < _lines.Length)
            {
                if (string.IsNullOrWhiteSpace(_lines[_index]))
                {
                    _index++;
                    continue;
                }

                var currentIndent = CountIndent(_lines[_index]);

                if (currentIndent < indent)
                {
                    break;
                }

                if (currentIndent != indent)
                {
                    AddIssue("frontmatter.parse", $"Unexpected indentation on line {_index + 1}.", $"frontmatter.line.{_index + 1}");
                    return values;
                }

                var line = _lines[_index][currentIndent..];

                if (line.StartsWith("- ", StringComparison.Ordinal))
                {
                    AddIssue("frontmatter.parse", $"Sequence items are not allowed at this level on line {_index + 1}.", $"frontmatter.line.{_index + 1}");
                    return values;
                }

                var separatorIndex = line.IndexOf(':');

                if (separatorIndex <= 0)
                {
                    AddIssue("frontmatter.parse", $"Expected a 'key: value' entry on line {_index + 1}.", $"frontmatter.line.{_index + 1}");
                    return values;
                }

                var key = line[..separatorIndex].Trim();
                var rawValue = line[(separatorIndex + 1)..].Trim();
                _index++;

                if (string.IsNullOrWhiteSpace(key))
                {
                    AddIssue("frontmatter.parse", $"Frontmatter key cannot be empty on line {_index}.", $"frontmatter.line.{_index}");
                    return values;
                }

                if (rawValue.Length > 0)
                {
                    values[key] = ParseScalar(rawValue);
                    continue;
                }

                if (_index >= _lines.Length || string.IsNullOrWhiteSpace(_lines[_index]))
                {
                    AddIssue("frontmatter.parse", $"Expected nested values for '{key}'.", $"frontmatter.line.{_index}");
                    return values;
                }

                var nextIndent = CountIndent(_lines[_index]);

                if (nextIndent <= indent)
                {
                    AddIssue("frontmatter.parse", $"Expected an indented nested block for '{key}'.", $"frontmatter.line.{_index + 1}");
                    return values;
                }

                var nextContent = _lines[_index][nextIndent..];
                values[key] = nextContent.StartsWith("- ", StringComparison.Ordinal)
                    ? ParseSequence(nextIndent, depth + 1)
                    : ParseMapping(nextIndent, depth + 1);
            }

            return values;
        }

        private List<object?> ParseSequence(int indent, int depth)
        {
            var values = new List<object?>();

            if (DepthExceeded(depth))
            {
                return values;
            }

            while (_index < _lines.Length)
            {
                if (string.IsNullOrWhiteSpace(_lines[_index]))
                {
                    _index++;
                    continue;
                }

                var currentIndent = CountIndent(_lines[_index]);

                if (currentIndent < indent)
                {
                    break;
                }

                if (currentIndent != indent)
                {
                    AddIssue("frontmatter.parse", $"Unexpected indentation inside sequence on line {_index + 1}.", $"frontmatter.line.{_index + 1}");
                    return values;
                }

                var line = _lines[_index][currentIndent..];

                if (!line.StartsWith("- ", StringComparison.Ordinal))
                {
                    AddIssue("frontmatter.parse", $"Expected a sequence item on line {_index + 1}.", $"frontmatter.line.{_index + 1}");
                    return values;
                }

                var rawValue = line[2..].Trim();

                if (rawValue.Length == 0)
                {
                    AddIssue("frontmatter.parse", $"Nested complex sequence items are not supported on line {_index + 1}.", $"frontmatter.line.{_index + 1}");
                    return values;
                }

                values.Add(ParseScalar(rawValue));
                _index++;
            }

            return values;
        }

        private static object? ParseScalar(string rawValue)
        {
            if (rawValue == "[]")
            {
                return new List<object?>();
            }

            if (bool.TryParse(rawValue, out var boolValue))
            {
                return boolValue;
            }

            if (int.TryParse(rawValue, out var intValue))
            {
                return intValue;
            }

            if ((rawValue.StartsWith('"') && rawValue.EndsWith('"')) ||
                (rawValue.StartsWith('\'') && rawValue.EndsWith('\'')))
            {
                return rawValue[1..^1];
            }

            return rawValue;
        }

        private static int CountIndent(string line)
        {
            var count = 0;

            foreach (var character in line)
            {
                if (character == ' ')
                {
                    count++;
                    continue;
                }

                if (character == '\t')
                {
                    return count + 1;
                }

                break;
            }

            return count;
        }

        private bool DepthExceeded(int depth)
        {
            if (depth <= MaxDepth)
            {
                return false;
            }

            var lineNumber = Math.Min(_index + 1, _lines.Length);
            AddIssue(
                "frontmatter.parse.depth_exceeded",
                $"Frontmatter nesting exceeds maximum depth of {MaxDepth} on line {lineNumber}.",
                $"frontmatter.line.{lineNumber}");
            return true;
        }

        private void AddIssue(string code, string message, string path)
        {
            _issues.Add(new ArtifactValidationIssue(code, message, path));
        }
    }
}
