﻿using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace Miniscript {
	public class Parser {

		public string errorContext;	// name of file, etc., used for error reporting
		//public int lineNum;			// which line number we're currently parsing

		// BackPatch: represents a place where we need to patch the code to fill
		// in a jump destination (once we figure out where that destination is).
		class BackPatch {
			public int lineNum;			// which code line to patch
			public string waitingFor;	// what keyword we're waiting for (e.g., "end if")
		}

		// JumpPoint: represents a place in the code we will need to jump to later
		// (typically, the top of a loop of some sort).
		class JumpPoint {
			public int lineNum;			// line number to jump to		
			public string keyword;		// jump type, by keyword: "while", "for", etc.
		}

		class ParseState {
			public List<TAC.Line> code = new List<TAC.Line>();
			public List<BackPatch> backpatches = new List<BackPatch>();
			public List<JumpPoint> jumpPoints = new List<JumpPoint>();
			public int nextTempNum = 0;

			public void Add(TAC.Line line) {
//				Console.WriteLine("ADDING line " + code.Count + ": " + line);
				code.Add(line);
			}

			/// <summary>
			/// Add the last code line as a backpatch point, to be patched
			/// (in rhsA) when we encounter a line with the given waitFor.
			/// </summary>
			/// <param name="waitFor">Wait for.</param>
			public void AddBackpatch(string waitFor) {
				backpatches.Add(new BackPatch() { lineNum=code.Count-1, waitingFor=waitFor });
			}

			public void AddJumpPoint(string jumpKeyword) {
				jumpPoints.Add(new JumpPoint() { lineNum = code.Count, keyword = jumpKeyword });
			}

			public JumpPoint CloseJumpPoint(string keyword) {
				int idx = jumpPoints.Count - 1;
				if (idx < 0 || jumpPoints[idx].keyword != keyword) {
					throw new CompilerException(string.Format("'end {0}' without matching '{0}'", keyword));
				}
				JumpPoint result = jumpPoints[idx];
				jumpPoints.RemoveAt(idx);
				return result;
			}

			/// <summary>
			/// Call this method when we've found an 'end' keyword, and want
			/// to patch up any jumps that were waiting for that.  Patch the
			/// matching backpatch (and any after it) to the current code end.
			/// </summary>
			/// <param name="keywordFound">Keyword found.</param>
			/// <param name="reservingLines">Extra lines (after the current position) to patch to.</param> 
			public void Patch(string keywordFound, int reservingLines=0) {
				Patch(keywordFound, null, reservingLines);
			}

			/// <summary>
			/// Call this method when we've found an 'end' keyword, and want
			/// to patch up any jumps that were waiting for that.  Patch the
			/// matching backpatch (and any after it) to the current code end.
			/// </summary>
			/// <param name="keywordFound">Keyword found.</param>
			/// <param name="alsoPatch">Keyword to also patch, if we see it before keywordFound.</param> 
			/// <param name="reservingLines">Extra lines (after the current position) to patch to.</param> 
			public void Patch(string keywordFound, string alsoPatch, int reservingLines=0) {
				// Start by finding the matching backpatch.
				int idx;
				for (idx = backpatches.Count - 1; idx >= 0; idx--) {
					if (backpatches[idx].waitingFor == keywordFound) break;
					if (alsoPatch == null || backpatches[idx].waitingFor != alsoPatch) {
						throw new CompilerException("'" + keywordFound + "' skips expected '" + backpatches[idx].waitingFor + "'");
					}
				}
				// Make sure we found one...
				if (idx < 0) throw new CompilerException("'" + keywordFound + "' without matching block starter");

				// Now, patch all from there to the end.
				Value target = TAC.Num(code.Count + reservingLines);
				for (int i = backpatches.Count - 1; i >= idx; i--) {
					code[backpatches[i].lineNum].rhsA = target;
				}
				backpatches.RemoveRange(idx, backpatches.Count - idx);
			}

			/// <summary>
			/// Patches up all the branches for a single open if block.  That includes
			/// the last "else" block, as well as one or more "end if" jumps.
			/// </summary>
			public void PatchIfBlock() {
				Value target = TAC.Num(code.Count);

				int idx = backpatches.Count - 1;
				while (idx >= 0) {
					BackPatch 
					bp = backpatches[idx];
					if (bp.waitingFor == "if:MARK") {
						// There's the special marker that indicates the true start of this if block.
						backpatches.RemoveAt(idx);
						return;
					} else if (bp.waitingFor == "end if" || bp.waitingFor == "else") {
						code[bp.lineNum].rhsA = target;
						backpatches.RemoveAt(idx);
					}
					idx--;
				}
			}
		}

		// List of open code blocks we're working on (while compiling a function,
		// we push a new one onto this stack, compile to that, and then pop it
		// off when we reach the end of the function).
		Stack<ParseState> outputStack;

		// Handy reference to the top of outputStack.
		ParseState output;

		// A new parse state that needs to be pushed onto the stack, as soon as we
		// finish with the current line we're working on:
		ParseState pendingState = null;

		public Parser() {
			Reset();
		}

		public void Reset() {
			output = new ParseState();
			if (outputStack == null) outputStack = new Stack<ParseState>();
			else outputStack.Clear();
			outputStack.Push(output);
		}

		public bool NeedMoreInput() {
			if (outputStack.Count > 1) return true;
			if (output.backpatches.Count > 0) return true;
			return false;
		}

		public void Parse(string sourceCode, bool replMode=false) {
			Lexer tokens = new Lexer(sourceCode);
			ParseMultipleLines(tokens);

			if (!replMode && NeedMoreInput()) {
				// Whoops, we need more input but we don't have any.  This is an error.
				tokens.lineNum++;	// (so we report PAST the last line, making it clear this is an EOF problem)
				if (outputStack.Count > 1) {
					throw new CompilerException(errorContext, tokens.lineNum,
						"'function' without matching 'end function'");
				} else if (output.backpatches.Count > 0) {
					BackPatch bp = output.backpatches[output.backpatches.Count - 1];
					string msg;
					switch (bp.waitingFor) {
					case "end for":
						msg = "'for' without matching 'end for'";
						break;
					case "end if":
						msg = "'if' without matching 'end if'";
						break;
					case "end while":
						msg = "'while' without matching 'end while'";
						break;
					default:
						msg = "unmatched block opener";
						break;
					}
					throw new CompilerException(errorContext, tokens.lineNum, msg);
				}
			}
		}

		public TAC.Machine CreateVM(TextOutputMethod standardOutput) {
			TAC.Context root = new TAC.Context(output.code);
			return new TAC.Machine(root, standardOutput);
		}

		public void REPL(string line) {
			Parse(line);
			TAC.Dump(output.code);

			TAC.Machine vm = CreateVM(null);
			while (!vm.done) vm.Step();
		}

		delegate Value ExpressionParsingMethod(Lexer tokens, bool asLval=false, bool statementStart=false);

		/// <summary>
		/// Parse multiple statements until we run out of tokens, or reach 'end function'.
		/// </summary>
		/// <param name="tokens">Tokens.</param>
		void ParseMultipleLines(Lexer tokens) {
			while (!tokens.AtEnd) {
				// Skip any blank lines
				if (tokens.Peek().type == Token.Type.EOL) {
					tokens.Dequeue();
					continue;
				}

				// Prepare a source code location for error reporting
				SourceLoc location = new SourceLoc(errorContext, tokens.lineNum);

				// Pop our context if we reach 'end function'.
				if (tokens.Peek().type == Token.Type.Keyword && tokens.Peek().text == "end function") {
					tokens.Dequeue();
					if (outputStack.Count > 1) {
//						Console.WriteLine("Popping compiler output stack");
						outputStack.Pop();
						output = outputStack.Peek();
					} else {
						CompilerException e = new CompilerException("'end function' without matching block starter");
						e.location = location;
						throw e;
					}
					continue;
				}

				// Parse one line (statement).
				int outputStart = output.code.Count;
				try {
					ParseStatement(tokens);
				} catch (MiniscriptException mse) {
					if (mse.location == null) mse.location = location;
					throw mse;
				}
				// Fill in the location info for all the TAC lines we just generated.
				for (int i = outputStart; i < output.code.Count; i++) {
					output.code[i].location = location;
				}
			}
		}

		void ParseStatement(Lexer tokens) {
			if (tokens.Peek().type == Token.Type.Keyword) {
				// Handle statements that begin with a keyword.
				string keyword = tokens.Dequeue().text;
				switch (keyword) {
				case "return":
					{
						Value returnValue = ParseExpr(tokens);
						output.Add(new TAC.Line(TAC.LTemp(0), TAC.Line.Op.ReturnA, returnValue));
					}
					break;
				case "if":
					{
						Value condition = ParseExpr(tokens);
						RequireToken(tokens, Token.Type.Keyword, "then");
						// OK, now we need to emit a conditional branch, but keep track of this
						// on a stack so that when we get the corresponding "else" or  "end if", 
						// we can come back and patch that jump to the right place.
						output.Add(new TAC.Line(null, TAC.Line.Op.GotoAifNotB, null, condition));

						// ...but if blocks also need a special marker in the backpack stack
						// so we know where to stop when patching up (possibly multiple) 'end if' jumps.
						// We'll push a special dummy backpatch here that we look for in PatchIfBlock.
						output.AddBackpatch("if:MARK");

						output.AddBackpatch("else");
					}
					break;
				case "else":
					{
						// Back-patch the open if block, but leaving room for the jump:
						// Emit the jump from the current location, which is the end of an if-block,
						// to the end of the else block (which we'll have to back-patch later).
						output.Add(new TAC.Line(null, TAC.Line.Op.GotoA, null));
						// Back-patch the previously open if-block to jump here (right past the goto).
						output.Patch("else");
						// And open a new back-patch for this goto (which will jump all the way to the end if).
						output.AddBackpatch("end if");
					}
					break;
				case "else if":
					{
						output.Add(new TAC.Line(null, TAC.Line.Op.GotoA, null));
						output.Patch("else");
						output.AddBackpatch("end if");

						Value condition = ParseExpr(tokens);
						RequireToken(tokens, Token.Type.Keyword, "then");
						output.Add(new TAC.Line(null, TAC.Line.Op.GotoAifNotB, null, condition));
						output.AddBackpatch("else");
					}
					break;
				case "end if":
					// OK, this is tricky.  We might have an open "else" block or we might not.
					// And, we might have multiple open "end if" jumps (one for the if part,
					// and another for each else-if part).  Patch all that as a special case.
					output.PatchIfBlock();
					break;
				case "while":
					{
						// We need to note the current line, so we can jump back up to it at the end.
						output.AddJumpPoint(keyword);

						// Then parse the condition.
						Value condition = ParseExpr(tokens);

						// OK, now we need to emit a conditional branch, but keep track of this
						// on a stack so that when we get the corresponding "end while", 
						// we can come back and patch that jump to the right place.
						output.Add(new TAC.Line(null, TAC.Line.Op.GotoAifNotB, null, condition));
						output.AddBackpatch("end while");
					}
					break;
				case "end while":
					{
						// Unconditional jump back to the top of the while loop.
						JumpPoint jump = output.CloseJumpPoint("while");
						output.Add(new TAC.Line(null, TAC.Line.Op.GotoA, TAC.Num(jump.lineNum)));
						// Then, backpatch the open "while" branch to here, right after the loop.
						// And also patch any "break" branches emitted after that point.
						output.Patch(keyword, "break");
					}
					break;
				case "for":
					{
						// Get the loop variable, "in" keyword, and expression to loop over.
						// (Note that the expression is only evaluated once, before the loop.)
						Token loopVarTok = RequireToken(tokens, Token.Type.Identifier);
						ValVar loopVar = new ValVar(loopVarTok.text);
						RequireToken(tokens, Token.Type.Keyword, "in");
						Value stuff = ParseExpr(tokens);
						if (stuff == null) {
							throw new CompilerException(errorContext, tokens.lineNum,
								"sequence expression expected for 'for' loop");
						}

						// Create an index variable to iterate over the sequence, initialized to -1.
						ValVar idxVar = new ValVar("__" + loopVarTok.text + "_idx");
						output.Add(new TAC.Line(idxVar, TAC.Line.Op.AssignA, TAC.Num(-1)));

						// We need to note the current line, so we can jump back up to it at the end.
						output.AddJumpPoint(keyword);

						// Now increment the index variable, and branch to the end if it's too big.
						// (We'll have to backpatch this branch later.)
						output.Add(new TAC.Line(idxVar, TAC.Line.Op.APlusB, idxVar, TAC.Num(1)));
						ValTemp sizeOfSeq = new ValTemp(output.nextTempNum++);
						output.Add(new TAC.Line(sizeOfSeq, TAC.Line.Op.LengthOfA, stuff));
						ValTemp isTooBig = new ValTemp(output.nextTempNum++);
						output.Add(new TAC.Line(isTooBig, TAC.Line.Op.AGreatOrEqualB, idxVar, sizeOfSeq));
						output.Add(new TAC.Line(null, TAC.Line.Op.GotoAifB, null, isTooBig));
						output.AddBackpatch("end for");

						// Otherwise, get the sequence value into our loop variable.
						output.Add(new TAC.Line(loopVar, TAC.Line.Op.ElemBofIterA, stuff, idxVar));
					}
					break;
				case "end for":
					{
						// Unconditional jump back to the top of the for loop.
						JumpPoint jump = output.CloseJumpPoint("for");
						output.Add(new TAC.Line(null, TAC.Line.Op.GotoA, TAC.Num(jump.lineNum)));
						// Then, backpatch the open "for" branch to here, right after the loop.
						// And also patch any "break" branches emitted after that point.
						output.Patch(keyword, "break");
					}
					break;
				case "break":
					{
						// Emit a jump to the end, to get patched up later.
						output.Add(new TAC.Line(null, TAC.Line.Op.GotoA));
						output.AddBackpatch("break");
					}
					break;
				case "continue":
					{
						// Jump unconditionally back to the current open jump point.
						if (output.jumpPoints.Count == 0) {
							throw new CompilerException(errorContext, tokens.lineNum,
								"'continue' without open loop block");
						}
						JumpPoint jump = output.jumpPoints.Last();
						output.Add(new TAC.Line(null, TAC.Line.Op.GotoA, TAC.Num(jump.lineNum)));
					}
					break;
				default:
					throw new CompilerException(errorContext, tokens.lineNum,
						"unexpected keyword '" + keyword + "' at start of line");
				}
			} else {
				ParseAssignment(tokens);
			}

			// A statement should consume everything to the end of the line.
			RequireToken(tokens, Token.Type.EOL);

			// Finally, if we have a pending state, because we encountered a function(),
			// then push it onto our stack now that we're done with that statement.
			if (pendingState != null) {
//				Console.WriteLine("PUSHING NEW PARSE STATE");
				output = pendingState;
				outputStack.Push(output);
				pendingState = null;
			}

		}

		void ParseAssignment(Lexer tokens) {
			Value expr = ParseExpr(tokens, true, true);
			Value lhs, rhs;
			switch (tokens.Peek().type) {
			case Token.Type.EOL:
				// No explicit assignment; store an implicit result
				rhs = FullyEvaluate(expr);
				output.Add(new TAC.Line(null, TAC.Line.Op.AssignImplicit, rhs));
				return;
			case Token.Type.OpAssign:
				tokens.Dequeue();	// skip '='
				lhs = expr;
				rhs = ParseExpr(tokens);
				break;
			default:
				RequireEitherToken(tokens, Token.Type.EOL, Token.Type.OpAssign);
				return;
			}

			// OK, now, in many cases our last TAC line at this point
			// is an assignment to our rhs temp.  In that case, as
			// a simple (but very useful) optimization, we can simply
			// patch that to assign to our lhs instead.
			if (rhs is ValTemp && output.code.Count > 0) {
				TAC.Line line = output.code[output.code.Count - 1];
				if (line.lhs.Equals(rhs)) {
					// Yep, that's the case.  Patch it up.
					line.lhs = lhs;
					return;
				}
			}
			// In any other case, do an assignment statement to our lhs.
			output.Add(new TAC.Line(lhs, TAC.Line.Op.AssignA, rhs));
		}

		Value ParseExpr(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParseFunction;
			return nextLevel(tokens, asLval, statementStart);
		}

		Value ParseFunction(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParseOr;
			Token tok = tokens.Peek();
			if (tok.type != Token.Type.Keyword || tok.text != "function") return nextLevel(tokens, asLval, statementStart);
			tokens.Dequeue();

			RequireToken(tokens, Token.Type.LParen);

			Function func = new Function(null);

			while (tokens.Peek().type != Token.Type.RParen) {
				// parse a parameter: a comma-separated list of
				//			identifier
				//	or...	identifier = expr
				Token id = tokens.Dequeue();
				if (id.type != Token.Type.Identifier) throw new CompilerException(errorContext, tokens.lineNum,
					"got " + id + " where an identifier is required");
				Value defaultValue = null;
				if (tokens.Peek().type == Token.Type.OpAssign) {
					tokens.Dequeue();	// skip '='
					defaultValue = ParseExpr(tokens);
				}
				func.parameters.Add(new Function.Param(id.text, defaultValue));
				if (tokens.Peek().type == Token.Type.RParen) break;
				RequireToken(tokens, Token.Type.Comma);
			}

			RequireToken(tokens, Token.Type.RParen);

			// Now, we need to parse the function body into its own parsing context.
			// But don't push it yet -- we're in the middle of parsing some expression
			// or statement in the current context, and need to finish that.
			if (pendingState != null) throw new CompilerException(errorContext, tokens.lineNum,
				"can't start two functions in one statement");
			pendingState = new ParseState();

//			Console.WriteLine("STARTED FUNCTION");

			// Create a function object attached to the new parse state code.
			func.code = pendingState.code;
			return new ValFunction(func);
		}

		Value ParseOr(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParseAnd;
			Value val = nextLevel(tokens, asLval, statementStart);
			List<TAC.Line> jumpLines = null;
			Token tok = tokens.Peek();
			while (tok.type == Token.Type.Keyword && tok.text == "or") {
				tokens.Dequeue();		// discard "or"
				val = FullyEvaluate(val);

				// Set up a shart-circuit jump based on the current value; 
				// we'll fill in the jump destination later.  Note that the
				// usual GotoAifB opcode won't work here, without breaking
				// our calculation of intermediate truth.  We need to jump
				// only if our truth value is >= 1 (i.e. absolutely true).
				TAC.Line jump = new TAC.Line(null, TAC.Line.Op.GotoAifTrulyB, null, val);
				output.Add(jump);
				if (jumpLines == null) jumpLines = new List<TAC.Line>();
				jumpLines.Add(jump);

				Value opB = nextLevel(tokens);
				int tempNum = output.nextTempNum++;
				output.Add(new TAC.Line(TAC.LTemp(tempNum), TAC.Line.Op.AOrB, val, opB));
				val = TAC.RTemp(tempNum);

				tok = tokens.Peek();
			}

			// Now, if we have any short-circuit jumps, those are going to need
			// to copy the short-circuit result (always 1) to our output temp.
			// And anything else needs to skip over that.  So:
			if (jumpLines != null) {
				output.Add(new TAC.Line(null, TAC.Line.Op.GotoA, TAC.Num(output.code.Count+2)));	// skip over this line:
				output.Add(new TAC.Line(val, TAC.Line.Op.AssignA, ValNumber.one));	// result = 1
				foreach (TAC.Line jump in jumpLines) {
					jump.rhsA = TAC.Num(output.code.Count-1);	// short-circuit to the above result=1 line
				}
			}

			return val;
		}

		Value ParseAnd(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParseNot;
			Value val = nextLevel(tokens, asLval, statementStart);
			List<TAC.Line> jumpLines = null;
			Token tok = tokens.Peek();
			while (tok.type == Token.Type.Keyword && tok.text == "and") {
				tokens.Dequeue();		// discard "and"
				val = FullyEvaluate(val);

				// Set up a short-circuit jump based on the current value; 
				// we'll fill in the jump destination later.
				TAC.Line jump = new TAC.Line(null, TAC.Line.Op.GotoAifNotB, null, val);
				output.Add(jump);
				if (jumpLines == null) jumpLines = new List<TAC.Line>();
				jumpLines.Add(jump);

				Value opB = nextLevel(tokens, asLval, statementStart);
				int tempNum = output.nextTempNum++;
				output.Add(new TAC.Line(TAC.LTemp(tempNum), TAC.Line.Op.AAndB, val, opB));
				val = TAC.RTemp(tempNum);

				tok = tokens.Peek();
			}

			// Now, if we have any short-circuit jumps, those are going to need
			// to copy the short-circuit result (always 0) to our output temp.
			// And anything else needs to skip over that.  So:
			if (jumpLines != null) {
				output.Add(new TAC.Line(null, TAC.Line.Op.GotoA, TAC.Num(output.code.Count+2)));	// skip over this line:
				output.Add(new TAC.Line(val, TAC.Line.Op.AssignA, ValNumber.zero));	// result = 0
				foreach (TAC.Line jump in jumpLines) {
					jump.rhsA = TAC.Num(output.code.Count-1);	// short-circuit to the above result=0 line
				}
			}

			return val;
		}

		Value ParseNot(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParseComparisons;
			Token tok = tokens.Peek();
			Value val;
			if (tok.type == Token.Type.Keyword && tok.text == "not") {
				tokens.Dequeue();		// discard "not"
				val = nextLevel(tokens);
				int tempNum = output.nextTempNum++;
				output.Add(new TAC.Line(TAC.LTemp(tempNum), TAC.Line.Op.NotA, val));
				val = TAC.RTemp(tempNum);
			} else {
				val = nextLevel(tokens, asLval, statementStart
				);
			}
			return val;
		}

		Value ParseComparisons(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParseAddSub;
			Value val = nextLevel(tokens, asLval, statementStart);
			Value opA = val;
			TAC.Line.Op opcode = ComparisonOp(tokens.Peek().type);
			// Parse a string of comparisons, all multiplied together
			// (so every comparison must be true for the whole expression to be true).
			bool firstComparison = true;
			while (opcode != TAC.Line.Op.Noop) {
				tokens.Dequeue();	// discard the operator (we have the opcode)
				//opA = FullyEvaluate(opA);

				Value opB = nextLevel(tokens);
				int tempNum = output.nextTempNum++;
				output.Add(new TAC.Line(TAC.LTemp(tempNum), opcode,	opA, opB));
				if (firstComparison) {
					firstComparison = false;
				} else {
					tempNum = output.nextTempNum++;
					output.Add(new TAC.Line(TAC.LTemp(tempNum), TAC.Line.Op.ATimesB, val, TAC.RTemp(tempNum - 1)));
				}
				val = TAC.RTemp(tempNum);
				opA = opB;
				opcode = ComparisonOp(tokens.Peek().type);
			}
			return val;
		}

		// Find the TAC operator that corresponds to the given token type,
		// for comparisons.  If it's not a comparison operator, return TAC.Line.Op.Noop.
		static TAC.Line.Op ComparisonOp(Token.Type tokenType) {
			switch (tokenType) {
			case Token.Type.OpEqual:		return TAC.Line.Op.AEqualB;
			case Token.Type.OpNotEqual:		return TAC.Line.Op.ANotEqualB;
			case Token.Type.OpGreater:		return TAC.Line.Op.AGreaterThanB;
			case Token.Type.OpGreatEqual:	return TAC.Line.Op.AGreatOrEqualB;
			case Token.Type.OpLesser:		return TAC.Line.Op.ALessThanB;
			case Token.Type.OpLessEqual:	return TAC.Line.Op.ALessOrEqualB;
			default: return TAC.Line.Op.Noop;
			}
		}

		Value ParseAddSub(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParseMultDiv;
			Value val = nextLevel(tokens, asLval, statementStart);
			Token tok = tokens.Peek();
			while (tok.type == Token.Type.OpPlus || tok.type == Token.Type.OpMinus) {
				tokens.Dequeue();
				val = FullyEvaluate(val);
				Value opB = nextLevel(tokens);
				int tempNum = output.nextTempNum++;
				output.Add(new TAC.Line(TAC.LTemp(tempNum), 
					tok.type == Token.Type.OpPlus ? TAC.Line.Op.APlusB : TAC.Line.Op.AMinusB,
					val, opB));
				val = TAC.RTemp(tempNum);

				tok = tokens.Peek();
			}
			return val;
		}

		Value ParseMultDiv(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParseUnaryMinus;
			Value val = nextLevel(tokens, asLval, statementStart);
			Token tok = tokens.Peek();
			while (tok.type == Token.Type.OpTimes || tok.type == Token.Type.OpDivide || tok.type == Token.Type.OpMod) {
				tokens.Dequeue();
				val = FullyEvaluate(val);
				Value opB = nextLevel(tokens);
				int tempNum = output.nextTempNum++;
				switch (tok.type) {
				case Token.Type.OpTimes:
					output.Add(new TAC.Line(TAC.LTemp(tempNum), TAC.Line.Op.ATimesB, val, opB));
					break;
				case Token.Type.OpDivide:
					output.Add(new TAC.Line(TAC.LTemp(tempNum), TAC.Line.Op.ADividedByB, val, opB));
					break;
				case Token.Type.OpMod:
					output.Add(new TAC.Line(TAC.LTemp(tempNum), TAC.Line.Op.AModB, val, opB));
					break;
				}
				val = TAC.RTemp(tempNum);

				tok = tokens.Peek();
			}
			return val;
		}
			
		Value ParseUnaryMinus(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParseNew;
			if (tokens.Peek().type != Token.Type.OpMinus) return nextLevel(tokens, asLval, statementStart);
			tokens.Dequeue();		// skip '-'
			Value val = nextLevel(tokens);
			if (val is ValNumber) {
				// If what follows is a numeric literal, just invert it and be done!
				ValNumber valnum = (ValNumber)val;
				valnum.value = -valnum.value;
				return valnum;
			}
			// Otherwise, subtract it from 0 and return a new temporary.
			int tempNum = output.nextTempNum++;
			output.Add(new TAC.Line(TAC.LTemp(tempNum), TAC.Line.Op.AMinusB, TAC.Num(0), val));

			return TAC.RTemp(tempNum);
		}

		Value ParseNew(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParseAddressOf;
			if (tokens.Peek().type != Token.Type.Keyword || tokens.Peek().text != "new") return nextLevel(tokens, asLval, statementStart);
			tokens.Dequeue();		// skip 'new'
			// Grab a reference to our __isa value
			Value isa = nextLevel(tokens);
			// Now, create a new map, and set __isa on it to that.
			// NOTE: we must be sure this map gets created at runtime, not here at parse time.
			// Since it is an immutable object, we need to return a different one each time
			// this code executes (in a loop, function, etc.).  So, we use Op.CopyA below!
			ValMap map = new ValMap();
			map.SetElem(ValString.magicIsA, isa);
			Value result = new ValTemp(output.nextTempNum++);
			output.Add(new TAC.Line(result, TAC.Line.Op.CopyA, map));
			return result;
		}

		Value ParseAddressOf(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParsePower;
			if (tokens.Peek().type != Token.Type.AddressOf) return nextLevel(tokens, asLval, statementStart);
			tokens.Dequeue();
			Value val = nextLevel(tokens, true, statementStart);
			return val;
		}

		Value ParsePower(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParseDotExpr;
			Value val = nextLevel(tokens, asLval, statementStart);
			Token tok = tokens.Peek();
			while (tok.type == Token.Type.OpPower) {
				tokens.Dequeue();
				val = FullyEvaluate(val);
				Value opB = nextLevel(tokens);
				int tempNum = output.nextTempNum++;
				output.Add(new TAC.Line(TAC.LTemp(tempNum), TAC.Line.Op.APowB, val, opB));
				val = TAC.RTemp(tempNum);

				tok = tokens.Peek();
			}
			return val;
		}


		Value FullyEvaluate(Value val) {
//			if (val is ValSeqElem) {
//				ValSeqElem vse = (ValSeqElem)val;
//				ValTemp temp = new ValTemp(output.nextTempNum++);
//				output.Add(new TAC.Line(temp, TAC.Line.Op.ElemBofA, vse.sequence, vse.index));
//				return temp;
//			}
			if (val is ValSeqElem || val is ValVar) {
				// Evaluate a variable (which might be a function we need to call).
				ValTemp temp = new ValTemp(output.nextTempNum++);
				output.Add(new TAC.Line(temp, TAC.Line.Op.CallFunctionA, val, ValNumber.zero));
				return temp;
			}
			return val;
		}

		Value ParseDotExpr(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParseCall;
			Value val = nextLevel(tokens, asLval, statementStart);
			if (tokens.Peek().type != Token.Type.Dot) return val;
			while (tokens.Peek().type == Token.Type.Dot) {
				tokens.Dequeue();	// discard '.'
				Token nextIdent = RequireToken(tokens, Token.Type.Identifier);
				if (val is ValSeqElem) {
					// We're chaining sequences here; look up (by invoking)
					// the previous part of the sequence, so we can build on it.
					val = ParseCallArgs(val, tokens);
				}
				val = new ValSeqElem(val, new ValString(nextIdent.text));
				if (tokens.Peek().type == Token.Type.LParen) {
					// If this new element is followed by parens, we need to
					// parse it as a call right away.
					val = ParseCallArgs(val, tokens);
				}
			}
			if (val is ValSeqElem && !asLval && !statementStart) {
				// Because ParseCall is now a higher-precedence operation,
				// it doesn't happen automatically when we get to the end
				// of the dot chain.  But we still want to do it, unless
				// we are in an lvalue context.
				val = ParseCallArgs(val, tokens);
			}
			return val;
		}

		Value ParseCall(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParseSeqLookup;
			Value val = nextLevel(tokens, asLval, statementStart);
			if (val is ValVar || val is ValSeqElem) {
				// Got a variable... it might refer to a function!
				if (!asLval || tokens.Peek().type == Token.Type.LParen) {
					// If followed by parens, definitely a function call, possibly with arguments!
					// If not, well, let's call it anyway unless we need an lvalue.
					val = ParseCallArgs(val, tokens);
				}
			}
			return val;
		}

		Value ParseSeqLookup(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParseMap;
			Value val = nextLevel(tokens, asLval, statementStart);

			while (tokens.Peek().type == Token.Type.LSquare) {
				tokens.Dequeue();	// discard '['
				val = FullyEvaluate(val);

				if (tokens.Peek().type == Token.Type.Colon) {	// e.g., foo[:4]
					tokens.Dequeue();	// discard ':'
					Value index2 = ParseExpr(tokens);
					ValTemp temp = new ValTemp(output.nextTempNum++);
					Intrinsics.CompileSlice(output.code, val, null, index2, temp.tempNum);
					val = temp;
				} else {
					Value index = ParseExpr(tokens);
					if (tokens.Peek().type == Token.Type.Colon) {	// e.g., foo[2:4] or foo[2:]
						tokens.Dequeue();	// discard ':'
						Value index2 = null;
						if (tokens.Peek().type != Token.Type.RSquare) index2 = ParseExpr(tokens);
						ValTemp temp = new ValTemp(output.nextTempNum++);
						Intrinsics.CompileSlice(output.code, val, index, index2, temp.tempNum);
						val = temp;
					} else {			// e.g., foo[3]  (not a slice at all)
						if (statementStart) {
							// At the start of a statement, we don't want to compile the
							// last sequence lookup, because we might have to convert it into
							// an assignment.  But we want to compile any previous one.
							if (val is ValSeqElem) {
								ValSeqElem vsVal = (ValSeqElem)val;
								ValTemp temp = new ValTemp(output.nextTempNum++);
								output.Add(new TAC.Line(temp, TAC.Line.Op.ElemBofA, vsVal.sequence, vsVal.index));
								val = temp;
							}
							val = new ValSeqElem(val, index);
						} else {
							// Anywhere else in an expression, we can compile the lookup right away.
							ValTemp temp = new ValTemp(output.nextTempNum++);
							output.Add(new TAC.Line(temp, TAC.Line.Op.ElemBofA, val, index));
							val = temp;
						}
					}
				}

				RequireToken(tokens, Token.Type.RSquare);
			}
			return val;
		}

		Value ParseMap(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParseList;
			if (tokens.Peek().type != Token.Type.LCurly) return nextLevel(tokens, asLval, statementStart);
			tokens.Dequeue();
			// NOTE: we must be sure this map gets created at runtime, not here at parse time.
			// Since it is an immutable object, we need to return a different one each time
			// this code executes (in a loop, function, etc.).  So, we use Op.CopyA below!
			ValMap map = new ValMap();
			if (tokens.Peek().type == Token.Type.RCurly) {
				tokens.Dequeue();
			} else while (true) {
				Value key = ParseExpr(tokens);
				if (key == null) throw new CompilerException(errorContext, tokens.lineNum,
						"expression required as map key");
				RequireToken(tokens, Token.Type.Colon);
				Value value = ParseExpr(tokens);
				if (value == null) throw new CompilerException(errorContext, tokens.lineNum,
						"expression required as map value");

				map.map.Add(key, value);
				
				if (RequireEitherToken(tokens, Token.Type.Comma, Token.Type.RCurly).type == Token.Type.RCurly) break;
			}
			Value result = new ValTemp(output.nextTempNum++);
			output.Add(new TAC.Line(result, TAC.Line.Op.CopyA, map));
			return result;
		}

		//		list	:= '[' expr [, expr, ...] ']'
		//				 | quantity
		Value ParseList(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParseQuantity;
			if (tokens.Peek().type != Token.Type.LSquare) return nextLevel(tokens, asLval, statementStart);
			tokens.Dequeue();
			// NOTE: we must be sure this list gets created at runtime, not here at parse time.
			// Since it is an immutable object, we need to return a different one each time
			// this code executes (in a loop, function, etc.).  So, we use Op.CopyA below!
			ValList list = new ValList();
			if (tokens.Peek().type == Token.Type.RSquare) {
				tokens.Dequeue();
			} else while (true) {
				Value elem = ParseExpr(tokens);
				if (elem == null) throw new CompilerException("expression required as list element");
				list.values.Add(elem);
				if (RequireEitherToken(tokens, Token.Type.Comma, Token.Type.RSquare).type == Token.Type.RSquare) break;
			}
			if (statementStart) return list;	// return the list as-is for indexed assignment (foo[3]=42)
			Value result = new ValTemp(output.nextTempNum++);
			output.Add(new TAC.Line(result, TAC.Line.Op.CopyA, list));	// use COPY on this mutable list!
			return result;
		}

		//		quantity := '(' expr ')'
		//				  | call
		Value ParseQuantity(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParseAtom;
			if (tokens.Peek().type != Token.Type.LParen) return nextLevel(tokens, asLval, statementStart);
			tokens.Dequeue();
			Value val = ParseExpr(tokens);
			RequireToken(tokens, Token.Type.RParen);
			return val;
		}

		/// <summary>
		/// Helper method that gathers arguments, emitting SetParamAasB for each one,
		/// and then emits the actual call to the given function.  It works both for
		/// a parenthesized set of arguments, and for no parens (i.e. no arguments).
		/// </summary>
		/// <returns>The call arguments.</returns>
		/// <param name="funcRef">Function to invoke.</param>
		/// <param name="tokens">Token stream.</param>
		Value ParseCallArgs(Value funcRef, Lexer tokens) {
			int argCount = 0;
			if (tokens.Peek().type == Token.Type.LParen) {
				tokens.Dequeue();		// remove '('
				if (tokens.Peek().type == Token.Type.RParen) {
					tokens.Dequeue();
				} else while (true) {
					Value arg = ParseExpr(tokens);
					output.Add(new TAC.Line(null, TAC.Line.Op.PushParam, arg));
					argCount++;
					if (RequireEitherToken(tokens, Token.Type.Comma, Token.Type.RParen).type == Token.Type.RParen) break;
				}
			}
			ValTemp result = new ValTemp(output.nextTempNum++);
			output.Add(new TAC.Line(result, TAC.Line.Op.CallFunctionA, funcRef, TAC.Num(argCount)));
			return result;
		}
			
		Value ParseAtom(Lexer tokens, bool asLval=false, bool statementStart=false) {
			Token tok = !tokens.AtEnd ? tokens.Dequeue() : Token.EOL;
			if (tok.type == Token.Type.Number) {
				return new ValNumber(double.Parse(tok.text));
			} else if (tok.type == Token.Type.String) {
				return new ValString(tok.text);
			} else if (tok.type == Token.Type.Identifier) {
				return new ValVar(tok.text);
			} else if (tok.type == Token.Type.Keyword && tok.text == "null") {
				return null;
			}
			throw new CompilerException(string.Format("got {0} where number, string, or identifier is required", tok));
		}


		/// <summary>
		/// The given token type and text is required. So, consume the next token,
		/// and if it doesn't match, throw an error.
		/// </summary>
		/// <param name="tokens">Token queue.</param>
		/// <param name="type">Required token type.</param>
		/// <param name="text">Required token text (if applicable).</param>
		Token RequireToken(Lexer tokens, Token.Type type, string text=null) {
			Token got = (tokens.AtEnd ? Token.EOL : tokens.Dequeue());
			if (got.type != type || (text != null && got.text != text)) {
				Token expected = new Token(type, text);
				throw new CompilerException(string.Format("got {0} where {1} is required", got, expected));
			}
			return got;
		}

		Token RequireEitherToken(Lexer tokens, Token.Type type1, string text1, Token.Type type2, string text2=null) {
			Token got = (tokens.AtEnd ? Token.EOL : tokens.Dequeue());
			if ((got.type != type1 && got.type != type2)
				|| ((text1 != null && got.text != text1) && (text2 != null && got.text != text2))) {
				Token expected1 = new Token(type1, text1);
				Token expected2 = new Token(type2, text2);
				throw new CompilerException(string.Format("got {0} where {1} or {2} is required", got, expected1, expected2));
			}
			return got;
		}

		Token RequireEitherToken(Lexer tokens, Token.Type type1, Token.Type type2, string text2=null) {
			return RequireEitherToken(tokens, type1, null, type2, text2);
		}

		public static void RunUnitTests() {
			Debug.WriteLine("\nTesting parser");
			Parser parser = new Parser();
			parser.Parse("x = 2; y = 3\nz=4+x*y");
			TAC.Dump(parser.output.code);
		}
	}
}

