﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
#if !CORECLR
using System.ComponentModel.Composition;
#endif
using System.Globalization;
using System.Linq;
using System.Management.Automation.Language;
using System.Text;
using Microsoft.Windows.PowerShell.ScriptAnalyzer.Generic;

namespace Microsoft.Windows.PowerShell.ScriptAnalyzer.BuiltinRules
{
    /// <summary>
    /// UseConsistentWhitespace: Checks if whitespace usage is consistent throughout the source file.
    /// </summary>
#if !CORECLR
    [Export(typeof(IScriptRule))]
#endif
    public class UseConsistentWhitespace : ConfigurableRule
    {
        private enum ErrorKind { BeforeOpeningBrace, Paren, Operator, SeparatorComma, SeparatorSemi,
            AfterOpeningBrace, BeforeClosingBrace, BeforePipe, AfterPipe, BetweenParameter };
        private const int whiteSpaceSize = 1;
        private const string whiteSpace = " ";
        private readonly SortedSet<TokenKind> openParenKeywordWhitelist = new SortedSet<TokenKind>()
        {
            TokenKind.If,
            TokenKind.ElseIf,
            TokenKind.Switch,
            TokenKind.For,
            TokenKind.Foreach,
            TokenKind.While
        };

        private List<Func<TokenOperations, IEnumerable<DiagnosticRecord>>> violationFinders
                = new List<Func<TokenOperations, IEnumerable<DiagnosticRecord>>>();

        [ConfigurableRuleProperty(defaultValue: true)]
        public bool CheckOpenBrace { get; protected set; }

        [ConfigurableRuleProperty(defaultValue: true)]
        public bool CheckInnerBrace { get; protected set; }

        [ConfigurableRuleProperty(defaultValue: true)]
        public bool CheckPipe { get; protected set; }

        [ConfigurableRuleProperty(defaultValue: false)]
        public bool CheckPipeForRedundantWhitespace { get; protected set; }

        [ConfigurableRuleProperty(defaultValue: true)]
        public bool CheckOpenParen { get; protected set; }

        [ConfigurableRuleProperty(defaultValue: true)]
        public bool CheckOperator { get; protected set; }

        [ConfigurableRuleProperty(defaultValue: true)]
        public bool CheckSeparator { get; protected set; }

        [ConfigurableRuleProperty(defaultValue: false)]
        public bool CheckParameter { get; protected set; }

        public override void ConfigureRule(IDictionary<string, object> paramValueMap)
        {
            base.ConfigureRule(paramValueMap);
            if (CheckOpenBrace)
            {
                violationFinders.Add(FindOpenBraceViolations);
            }

            if (CheckInnerBrace)
            {
                violationFinders.Add(FindInnerBraceViolations);
            }

            if (CheckPipe || CheckPipeForRedundantWhitespace)
            {
                violationFinders.Add(FindPipeViolations);
            }

            if (CheckOpenParen)
            {
                violationFinders.Add(FindOpenParenViolations);
            }

            if (CheckOperator)
            {
                violationFinders.Add(FindOperatorViolations);
            }

            if (CheckSeparator)
            {
                violationFinders.Add(FindSeparatorViolations);
            }
        }

        /// <summary>
        /// Analyzes the given ast to find the [violation]
        /// </summary>
        /// <param name="ast">AST to be analyzed. This should be non-null</param>
        /// <param name="fileName">Name of file that corresponds to the input AST.</param>
        /// <returns>A an enumerable type containing the violations</returns>
        public override IEnumerable<DiagnosticRecord> AnalyzeScript(Ast ast, string fileName)
        {
            if (ast == null)
            {
                throw new ArgumentNullException("ast");
            }

            var tokenOperations = new TokenOperations(Helper.Instance.Tokens, ast);
            var diagnosticRecords = Enumerable.Empty<DiagnosticRecord>();
            foreach (var violationFinder in violationFinders)
            {
                diagnosticRecords = diagnosticRecords.Concat(violationFinder(tokenOperations));
            }

            if (CheckParameter)
            {
                diagnosticRecords = diagnosticRecords.Concat(FindParameterViolations(ast));
            }

            return diagnosticRecords.ToArray(); // force evaluation here
        }

        /// <summary>
        /// Retrieves the common name of this rule.
        /// </summary>
        public override string GetCommonName()
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.UseConsistentWhitespaceCommonName);
        }

        /// <summary>
        /// Retrieves the description of this rule.
        /// </summary>
        public override string GetDescription()
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.UseConsistentWhitespaceDescription);
        }

        /// <summary>
        /// Retrieves the name of this rule.
        /// </summary>
        public override string GetName()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                Strings.NameSpaceFormat,
                GetSourceName(),
                Strings.UseConsistentWhitespaceName);
        }

        /// <summary>
        /// Retrieves the severity of the rule: error, warning or information.
        /// </summary>
        public override RuleSeverity GetSeverity()
        {
            return RuleSeverity.Warning;
        }

        /// <summary>
        /// Gets the severity of the returned diagnostic record: error, warning, or information.
        /// </summary>
        /// <returns></returns>
        public DiagnosticSeverity GetDiagnosticSeverity()
        {
            return DiagnosticSeverity.Warning;
        }

        /// <summary>
        /// Retrieves the name of the module/assembly the rule is from.
        /// </summary>
        public override string GetSourceName()
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.SourceName);
        }

        /// <summary>
        /// Retrieves the type of the rule, Builtin, Managed or Module.
        /// </summary>
        public override SourceType GetSourceType()
        {
            return SourceType.Builtin;
        }

        private string GetError(ErrorKind kind)
        {
            switch (kind)
            {
                case ErrorKind.BeforeOpeningBrace:
                    return string.Format(CultureInfo.CurrentCulture, Strings.UseConsistentWhitespaceErrorBeforeOpeningBrace);
                case ErrorKind.AfterOpeningBrace:
                    return string.Format(CultureInfo.CurrentCulture, Strings.UseConsistentWhitespaceErrorAfterOpeningBrace);
                case ErrorKind.BeforeClosingBrace:
                    return string.Format(CultureInfo.CurrentCulture, Strings.UseConsistentWhitespaceErrorBeforeClosingInnerBrace);
                case ErrorKind.Operator:
                    return string.Format(CultureInfo.CurrentCulture, Strings.UseConsistentWhitespaceErrorOperator);
                case ErrorKind.BeforePipe:
                    return string.Format(CultureInfo.CurrentCulture, Strings.UseConsistentWhitespaceErrorSpaceBeforePipe);
                case ErrorKind.AfterPipe:
                    return string.Format(CultureInfo.CurrentCulture, Strings.UseConsistentWhitespaceErrorSpaceAfterPipe);
                case ErrorKind.SeparatorComma:
                    return string.Format(CultureInfo.CurrentCulture, Strings.UseConsistentWhitespaceErrorSeparatorComma);
                case ErrorKind.SeparatorSemi:
                    return string.Format(CultureInfo.CurrentCulture, Strings.UseConsistentWhitespaceErrorSeparatorSemi);
                case ErrorKind.BetweenParameter:
                    return string.Format(CultureInfo.CurrentCulture, Strings.UseConsistentWhitespaceErrorSpaceBetweenParameter);
                default:
                    return string.Format(CultureInfo.CurrentCulture, Strings.UseConsistentWhitespaceErrorBeforeParen);
            }
        }

        private IEnumerable<DiagnosticRecord> FindOpenBraceViolations(TokenOperations tokenOperations)
        {
            foreach (var lcurly in tokenOperations.GetTokenNodes(TokenKind.LCurly))
            {

                if (lcurly.Previous == null
                    || !IsPreviousTokenOnSameLine(lcurly)
                    || lcurly.Previous.Value.Kind == TokenKind.LCurly
                    || ((lcurly.Previous.Value.TokenFlags & TokenFlags.MemberName) == TokenFlags.MemberName))
                {
                    continue;
                }

                if (!IsPreviousTokenApartByWhitespace(lcurly))
                {
                    yield return new DiagnosticRecord(
                        GetError(ErrorKind.BeforeOpeningBrace),
                        lcurly.Value.Extent,
                        GetName(),
                        GetDiagnosticSeverity(),
                        tokenOperations.Ast.Extent.File,
                        null,
                        GetCorrections(lcurly.Previous.Value, lcurly.Value, lcurly.Next.Value, false, true).ToList());
                }
            }
        }

        private IEnumerable<DiagnosticRecord> FindInnerBraceViolations(TokenOperations tokenOperations)
        {
            foreach (var lCurly in tokenOperations.GetTokenNodes(TokenKind.LCurly))
            {
                if (lCurly.Next == null
                    || !IsPreviousTokenOnSameLine(lCurly)
                    || lCurly.Next.Value.Kind == TokenKind.NewLine
                    || lCurly.Next.Value.Kind == TokenKind.LineContinuation
                    || lCurly.Next.Value.Kind == TokenKind.RCurly
                    )
                {
                    continue;
                }

                if (!IsNextTokenApartByWhitespace(lCurly))
                {
                    yield return new DiagnosticRecord(
                        GetError(ErrorKind.AfterOpeningBrace),
                        lCurly.Value.Extent,
                        GetName(),
                        GetDiagnosticSeverity(),
                        tokenOperations.Ast.Extent.File,
                        null,
                        GetCorrections(lCurly.Previous.Value, lCurly.Value, lCurly.Next.Value, true, false).ToList());
                }
            }

            foreach (var rCurly in tokenOperations.GetTokenNodes(TokenKind.RCurly))
            {
                if (rCurly.Previous == null
                    || !IsPreviousTokenOnSameLine(rCurly)
                    || rCurly.Previous.Value.Kind == TokenKind.LCurly
                    || rCurly.Previous.Value.Kind == TokenKind.NewLine
                    || rCurly.Previous.Value.Kind == TokenKind.LineContinuation
                    || rCurly.Previous.Value.Kind == TokenKind.AtCurly
                    )
                {
                    continue;
                }

                if (!IsPreviousTokenApartByWhitespace(rCurly))
                {
                    yield return new DiagnosticRecord(
                        GetError(ErrorKind.BeforeClosingBrace),
                        rCurly.Value.Extent,
                        GetName(),
                        GetDiagnosticSeverity(),
                        tokenOperations.Ast.Extent.File,
                        null,
                        GetCorrections(rCurly.Previous.Value, rCurly.Value, rCurly.Next.Value, false, true).ToList());
                }
            }
        }

        private IEnumerable<DiagnosticRecord> FindPipeViolations(TokenOperations tokenOperations)
        {
            foreach (var pipe in tokenOperations.GetTokenNodes(TokenKind.Pipe))
            {
                if (pipe.Next == null
                    || !IsPreviousTokenOnSameLine(pipe)
                    || pipe.Next.Value.Kind == TokenKind.Pipe
                    || pipe.Next.Value.Kind == TokenKind.NewLine
                    || pipe.Next.Value.Kind == TokenKind.LineContinuation
                    )
                {
                    continue;
                }

                if (!IsNextTokenApartByWhitespace(pipe, out bool hasRedundantWhitespace))
                {
                    if (CheckPipeForRedundantWhitespace && hasRedundantWhitespace || CheckPipe && !hasRedundantWhitespace)
                    {
                        yield return new DiagnosticRecord(
                            GetError(ErrorKind.AfterPipe),
                            pipe.Value.Extent,
                            GetName(),
                            GetDiagnosticSeverity(),
                            tokenOperations.Ast.Extent.File,
                            null,
                            GetCorrections(pipe.Previous.Value, pipe.Value, pipe.Next.Value, true, false).ToList());
                    }
                }
            }

            foreach (var pipe in tokenOperations.GetTokenNodes(TokenKind.Pipe))
            {
                if (pipe.Previous == null
                    || !IsPreviousTokenOnSameLine(pipe)
                    || pipe.Previous.Value.Kind == TokenKind.Pipe
                    || pipe.Previous.Value.Kind == TokenKind.NewLine
                    || pipe.Previous.Value.Kind == TokenKind.LineContinuation
                    )
                {
                    continue;
                }

                if (!IsPreviousTokenApartByWhitespace(pipe, out bool hasRedundantWhitespace))
                {
                    if (CheckPipeForRedundantWhitespace && hasRedundantWhitespace || CheckPipe && !hasRedundantWhitespace)
                    {
                        yield return new DiagnosticRecord(
                        GetError(ErrorKind.BeforePipe),
                        pipe.Value.Extent,
                        GetName(),
                        GetDiagnosticSeverity(),
                        tokenOperations.Ast.Extent.File,
                        null,
                        GetCorrections(pipe.Previous.Value, pipe.Value, pipe.Next.Value, false, true).ToList());
                    }
                }
            }
        }

        private IEnumerable<DiagnosticRecord> FindOpenParenViolations(TokenOperations tokenOperations)
        {
            foreach (var lparen in tokenOperations.GetTokenNodes(TokenKind.LParen))
            {
                if (lparen.Previous != null
                    && IsPreviousTokenOnSameLine(lparen)
                    && TokenTraits.HasTrait(lparen.Previous.Value.Kind, TokenFlags.Keyword)
                    && IsKeyword(lparen.Previous.Value)
                    && !IsPreviousTokenApartByWhitespace(lparen))
                {
                    yield return new DiagnosticRecord(
                        GetError(ErrorKind.Paren),
                        lparen.Value.Extent,
                        GetName(),
                        GetDiagnosticSeverity(),
                        tokenOperations.Ast.Extent.File,
                        null,
                        GetCorrections(lparen.Previous.Value, lparen.Value, lparen.Next.Value, false, true).ToList());
                }
            }
        }

        private IEnumerable<DiagnosticRecord> FindParameterViolations(Ast ast)
        {
            IEnumerable<Ast> commandAsts = ast.FindAll(
                    testAst => testAst is CommandAst, true);
            foreach (CommandAst commandAst in commandAsts)
            {
                List<Ast> commandParameterAstElements = commandAst.FindAll(
                    testAst => testAst.Parent == commandAst, searchNestedScriptBlocks: false).ToList();
                for (int i = 0; i < commandParameterAstElements.Count - 1; i++)
                {
                    IScriptExtent leftExtent = commandParameterAstElements[i].Extent;
                    IScriptExtent rightExtent = commandParameterAstElements[i + 1].Extent;
                    if (leftExtent.EndLineNumber != rightExtent.StartLineNumber)
                    {
                        continue;
                    }

                    var expectedStartColumnNumberOfRightExtent = leftExtent.EndColumnNumber + 1;
                    if (rightExtent.StartColumnNumber > expectedStartColumnNumberOfRightExtent)
                    {
                        int numberOfRedundantWhiteSpaces = rightExtent.StartColumnNumber - expectedStartColumnNumberOfRightExtent;
                        var correction = new CorrectionExtent(
                            startLineNumber: leftExtent.StartLineNumber,
                            endLineNumber: leftExtent.EndLineNumber,
                            startColumnNumber: leftExtent.EndColumnNumber + 1,
                            endColumnNumber: leftExtent.EndColumnNumber + 1 + numberOfRedundantWhiteSpaces,
                            text: string.Empty,
                            file: leftExtent.File);

                        yield return new DiagnosticRecord(
                            GetError(ErrorKind.BetweenParameter),
                            leftExtent,
                            GetName(),
                            GetDiagnosticSeverity(),
                            leftExtent.File,
                            suggestedCorrections: new CorrectionExtent[] { correction });
                    }
                }
            }
        }

        private bool IsSeparator(Token token)
        {
            return token.Kind == TokenKind.Comma || token.Kind == TokenKind.Semi;
        }

        private IEnumerable<DiagnosticRecord> FindSeparatorViolations(TokenOperations tokenOperations)
        {
            Func<LinkedListNode<Token>, bool> predicate = node =>
            {
                return node.Next != null
                    && node.Next.Value.Kind != TokenKind.NewLine
                    && node.Next.Value.Kind != TokenKind.EndOfInput // semicolon can be followed by end of input
                    && !IsPreviousTokenApartByWhitespace(node.Next);
            };

            foreach (var tokenNode in tokenOperations.GetTokenNodes(IsSeparator).Where(predicate))
            {
                var errorKind = tokenNode.Value.Kind == TokenKind.Comma
                    ? ErrorKind.SeparatorComma
                    : ErrorKind.SeparatorSemi;
                yield return getDiagnosticRecord(
                    tokenNode.Value,
                    errorKind,
                    GetCorrections(
                        tokenNode.Previous.Value,
                        tokenNode.Value,
                        tokenNode.Next.Value,
                        true,
                        false));
            }
        }

        private DiagnosticRecord getDiagnosticRecord(
            Token token,
            ErrorKind errKind,
            List<CorrectionExtent> corrections)
        {
            return new DiagnosticRecord(
                GetError(errKind),
                token.Extent,
                GetName(),
                GetDiagnosticSeverity(),
                token.Extent.File,
                null,
                corrections);
        }

        private bool IsKeyword(Token token)
        {
            return openParenKeywordWhitelist.Contains(token.Kind);
        }

        private static bool IsPreviousTokenApartByWhitespace(LinkedListNode<Token> tokenNode)
        {
            return IsPreviousTokenApartByWhitespace(tokenNode, out _);
        }

        private static bool IsPreviousTokenApartByWhitespace(LinkedListNode<Token> tokenNode, out bool hasRedundantWhitespace)
        {
            if (tokenNode.Value.Extent.StartLineNumber != tokenNode.Previous.Value.Extent.StartLineNumber)
            {
                hasRedundantWhitespace = false;
                return true;
            }
            var actualWhitespaceSize = tokenNode.Value.Extent.StartColumnNumber - tokenNode.Previous.Value.Extent.EndColumnNumber;
            hasRedundantWhitespace = actualWhitespaceSize - whiteSpaceSize > 0;
            return whiteSpaceSize == actualWhitespaceSize;
        }

        private static bool IsNextTokenApartByWhitespace(LinkedListNode<Token> tokenNode)
        {
            return IsNextTokenApartByWhitespace(tokenNode, out _);
        }

        private static bool IsNextTokenApartByWhitespace(LinkedListNode<Token> tokenNode, out bool hasRedundantWhitespace)
        {
            var actualWhitespaceSize = tokenNode.Next.Value.Extent.StartColumnNumber - tokenNode.Value.Extent.EndColumnNumber;
            hasRedundantWhitespace = actualWhitespaceSize - whiteSpaceSize > 0;
            return whiteSpaceSize == actualWhitespaceSize;
        }

        private bool IsPreviousTokenOnSameLineAndApartByWhitespace(LinkedListNode<Token> tokenNode)
        {
            return IsPreviousTokenOnSameLine(tokenNode) && IsPreviousTokenApartByWhitespace(tokenNode);
        }

        private bool IsPreviousTokenAdjacent(LinkedListNode<Token> tokenNode)
        {
            return tokenNode.Value.Extent.StartLineNumber == tokenNode.Previous.Value.Extent.EndLineNumber
                   && tokenNode.Value.Extent.StartColumnNumber == tokenNode.Previous.Value.Extent.EndColumnNumber;
        }

        private IEnumerable<DiagnosticRecord> FindOperatorViolations(TokenOperations tokenOperations)
        {
            foreach (LinkedListNode<Token> tokenNode in tokenOperations.GetTokenNodes((t)=>true))
            {
                bool tokenHasUnaryFlag = TokenTraits.HasTrait(tokenNode.Value.Kind, TokenFlags.UnaryOperator);
                bool tokenHasBinaryFlag = TokenTraits.HasTrait(tokenNode.Value.Kind, TokenFlags.BinaryOperator);
                bool checkLeftSide = false;
                bool checkRightSide = false;
                bool operatorIsPrefixOrPostfix = false;

                // Exclude operators handled by other UseConsistentWhitespace rule options
                if (tokenNode.Value.Kind == TokenKind.DotDot
                    || tokenNode.Value.Kind == TokenKind.Comma) {
                    continue;
                }
                // First check operators that have unary flag (may be unary or binary)
                else if (tokenHasUnaryFlag)
                {
                    Ast operatorAst = tokenOperations.GetAstPosition(tokenNode.Value);
                    // If both unary and binary flags are set, check type of AST node to determine which it is in this case.
                    if (tokenHasBinaryFlag && operatorAst is BinaryExpressionAst)
                    {
                        checkLeftSide = true;
                        checkRightSide = true;
                    }
                    else // Token must be unary operator.
                    {
                        operatorIsPrefixOrPostfix = TokenTraits.HasTrait(tokenNode.Value.Kind, TokenFlags.PrefixOrPostfixOperator)
                                                    || tokenNode.Value.Kind == TokenKind.Minus
                                                    || tokenNode.Value.Kind == TokenKind.Exclaim;
                        // If token and its AST node start at same position, operand is on the right.
                        if (tokenNode.Value.Extent.StartOffset == operatorAst.Extent.StartOffset)
                        {
                            checkRightSide = true;
                        }
                        else
                        {
                            checkLeftSide = true;
                        }
                    }
                }
                // Handle operators that are definitely binary
                else if (tokenHasBinaryFlag // binary flag is set but not unary
                         // include other (non-expression) binary operators
                         || TokenTraits.HasTrait(tokenNode.Value.Kind, TokenFlags.AssignmentOperator)
                         || tokenNode.Value.Kind == TokenKind.Redirection
                         || tokenNode.Value.Kind == TokenKind.AndAnd
                         || tokenNode.Value.Kind == TokenKind.OrOr
#if !(NET452 || PSV6)    // include both parts of ternary operator but only for PS7+
                         || TokenTraits.HasTrait(tokenNode.Value.Kind, TokenFlags.TernaryOperator)
                         || tokenNode.Value.Kind == TokenKind.Colon
#endif
                         ) {
                    checkLeftSide = true;
                    checkRightSide = true;
                }
                // Treat call and dot source operators as unary with operand on right.
                else if ((tokenNode.Value.Kind == TokenKind.Dot || tokenNode.Value.Kind == TokenKind.Ampersand)
                         && tokenOperations.GetAstPosition(tokenNode.Value) is CommandAst)
                {
                    checkRightSide = true;
                }
#if !(NET452)   // Treat background operator as unary with operand on left (only exists in PS6+)
                else if (tokenNode.Value.Kind == TokenKind.Ampersand)
                {
                    checkLeftSide = true;
                }
#endif
                else // Token is not an operator
                {
                    continue;
                }

                bool leftSideOK;
                bool rightSideOK;
                if (operatorIsPrefixOrPostfix)
                {
                    leftSideOK = !checkLeftSide || IsPreviousTokenAdjacent(tokenNode);
                    rightSideOK = !checkRightSide || IsPreviousTokenAdjacent(tokenNode.Next);
                }
                else
                {
                    leftSideOK = !checkLeftSide || IsPreviousTokenOnSameLineAndApartByWhitespace(tokenNode);

                    rightSideOK = !checkRightSide || tokenNode.Next.Value.Kind == TokenKind.NewLine
                                  || IsPreviousTokenOnSameLineAndApartByWhitespace(tokenNode.Next);
                }
                if (!leftSideOK || !rightSideOK)
                {
                    yield return new DiagnosticRecord(
                        GetError(ErrorKind.Operator),
                        tokenNode.Value.Extent,
                        GetName(),
                        GetDiagnosticSeverity(),
                        tokenOperations.Ast.Extent.File,
                        null,
                        GetCorrections(
                            tokenNode.Previous?.Value,
                            tokenNode.Value,
                            tokenNode.Next?.Value,
                            leftSideOK,
                            rightSideOK,
                            !operatorIsPrefixOrPostfix));
                }
            }
        }


        private List<CorrectionExtent> GetCorrections(
            Token prevToken,
            Token token,
            Token nextToken,
            bool leftSideOK,  // if this is false, then the returned correction extent will add a whitespace before the token
            bool rightSideOK, // if this is false, then the returned correction extent will add a whitespace after the token
            bool shouldHaveWhitespace = true // if this is false, then the returned correction extent will remove whitespace instead of adding it
            )
        {
            var sb = new StringBuilder();
            IScriptExtent e1 = token.Extent;
            if (!leftSideOK)
            {
                if (shouldHaveWhitespace)
                {
                    sb.Append(whiteSpace);
                }
                e1 = prevToken.Extent;
            }

            var e2 = token.Extent;
            if (!rightSideOK)
            {
                if (!leftSideOK)
                {
                    sb.Append(token.Text);
                }

                e2 = nextToken.Extent;
                if (shouldHaveWhitespace)
                {
                    sb.Append(whiteSpace);
                }
            }

            var extent = new ScriptExtent(
                new ScriptPosition(e1.File, e1.EndLineNumber, e1.EndColumnNumber, null),
                new ScriptPosition(e2.File, e2.StartLineNumber, e2.StartColumnNumber, null));
            return new List<CorrectionExtent>()
            {
                new CorrectionExtent(
                extent,
                sb.ToString(),
                token.Extent.File,
                GetError(ErrorKind.Operator))
            };
        }


        private bool IsPreviousTokenOnSameLine(LinkedListNode<Token> lparen)
        {
            return lparen.Previous.Value.Extent.EndLineNumber == lparen.Value.Extent.StartLineNumber;
        }

    }
}
