﻿using System;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;

namespace Miniscript {

	public static class TAC {

		public class Line {
			public enum Op {
				Noop = 0,
				AssignA,
				AssignImplicit,
				APlusB,
				AMinusB,
				ATimesB,
				ADividedByB,
				AModB,
				APowB,
				AEqualB,
				ANotEqualB,
				AGreaterThanB,
				AGreatOrEqualB,
				ALessThanB,
				ALessOrEqualB,
				AAndB,
				AOrB,
				CopyA,
				NotA,
				GotoA,
				GotoAifB,
				GotoAifTrulyB,
				GotoAifNotB,
				PushParam,
				CallFunctionA,
				CallIntrinsicA,
				ReturnA,
				ElemBofA,
				ElemBofIterA,
				LengthOfA
			}

			public Value lhs;
			public Op op;
			public Value rhsA;
			public Value rhsB;
//			public string comment;
			public SourceLoc location;

			public Line(Value lhs, Op op, Value rhsA=null, Value rhsB=null) {
				this.lhs = lhs;
				this.op = op;
				this.rhsA = rhsA;
				this.rhsB = rhsB;
			}
			
			public override int GetHashCode() {
				return lhs.GetHashCode() ^ op.GetHashCode() ^ rhsA.GetHashCode() ^ rhsB.GetHashCode() ^ location.GetHashCode();
			}
			
			public override bool Equals(object obj) {
				if (!(obj is Line)) return false;
				Line b = (Line)obj;
				return op == b.op && lhs == b.lhs && rhsA == b.rhsA && rhsB == b.rhsB && location == b.location;
			}
			
			public override string ToString() {
				string text;
				switch (op) {
				case Op.AssignA:
					text = string.Format("{0} := {1}", lhs, rhsA);
					break;
				case Op.AssignImplicit:
					text = string.Format("_ := {0}", rhsA);
					break;
				case Op.APlusB:
					text = string.Format("{0} := {1} + {2}", lhs, rhsA, rhsB);
					break;
				case Op.AMinusB:
					text = string.Format("{0} := {1} - {2}", lhs, rhsA, rhsB);
					break;
				case Op.ATimesB:
					text = string.Format("{0} := {1} * {2}", lhs, rhsA, rhsB);
					break;
				case Op.ADividedByB:
					text = string.Format("{0} := {1} / {2}", lhs, rhsA, rhsB);
					break;
				case Op.AModB:
					text = string.Format("{0} := {1} % {2}", lhs, rhsA, rhsB);
					break;
				case Op.APowB:
					text = string.Format("{0} := {1} ^ {2}", lhs, rhsA, rhsB);
					break;
				case Op.AEqualB:
					text = string.Format("{0} := {1} == {2}", lhs, rhsA, rhsB);
					break;
				case Op.ANotEqualB:
					text = string.Format("{0} := {1} != {2}", lhs, rhsA, rhsB);
					break;
				case Op.AGreaterThanB:
					text = string.Format("{0} := {1} > {2}", lhs, rhsA, rhsB);
					break;
				case Op.AGreatOrEqualB:
					text = string.Format("{0} := {1} >= {2}", lhs, rhsA, rhsB);
					break;
				case Op.ALessThanB:
					text = string.Format("{0} := {1} < {2}", lhs, rhsA, rhsB);
					break;
				case Op.ALessOrEqualB:
					text = string.Format("{0} := {1} <= {2}", lhs, rhsA, rhsB);
					break;
				case Op.AAndB:
					text = string.Format("{0} := {1} and {2}", lhs, rhsA, rhsB);
					break;
				case Op.AOrB:
					text = string.Format("{0} := {1} or {2}", lhs, rhsA, rhsB);
					break;
				case Op.CopyA:
					text = string.Format("{0} := copy of {1}", lhs, rhsA);
					break;
				case Op.NotA:
					text = string.Format("{0} := not {1}", lhs, rhsA);
					break;
				case Op.GotoA:
					text = string.Format("goto {0}", rhsA);
					break;
				case Op.GotoAifB:
					text = string.Format("goto {0} if {1}", rhsA, rhsB);
					break;
				case Op.GotoAifTrulyB:
					text = string.Format("goto {0} if truly {1}", rhsA, rhsB);
					break;
				case Op.GotoAifNotB:
					text = string.Format("goto {0} if not {1}", rhsA, rhsB);
					break;
				case Op.PushParam:
					text = string.Format("push param {0}", rhsA);
					break;
				case Op.CallFunctionA:
					text = string.Format("{0} := call {1} with {2} args", lhs, rhsA, rhsB);
					break;
				case Op.CallIntrinsicA:
					text = string.Format("intrinsic {0}", Intrinsic.GetByID(rhsA.IntValue()));
					break;
				case Op.ReturnA:
					text = string.Format("{0} := {1}; return", lhs, rhsA);
					break;
				case Op.ElemBofA:
					text = string.Format("{0} = {1}[{2}]", lhs, rhsA, rhsB);
					break;
				case Op.ElemBofIterA:
					text = string.Format("{0} = {1} iter {2}", lhs, rhsA, rhsB);
					break;
				case Op.LengthOfA:
					text = string.Format("{0} = len({1})", lhs, rhsA);
					break;
				default:
					throw new RuntimeException("unknown opcode: " + op);
					
				}
//				if (comment != null) text = text + "\t// " + comment;
				return text;
			}

			/// <summary>
			/// Evaluate this line and return the value that would be stored
			/// into the lhs.
			/// </summary>
			public Value Evaluate(Context context) {
				if (op == Op.AssignA || op == Op.ReturnA || op == Op.AssignImplicit) {
					// Assignment is a bit of a special case.  It's EXTREMELY common
					// in TAC, so needs to be efficient, but we have to watch out for
					// the case of a RHS that is a list or map.  This means it was a
					// literal in the source, and may contain references that need to
					// be evaluated now.
					if (rhsA is ValList || rhsA is ValMap) {
						return rhsA.FullEval(context);
					} else if (rhsA == null) {
						return null;
					} else {
						return rhsA.Val(context);
					}
				}
				if (op == Op.CopyA) {
					// This opcode is used for assigning a literal.  We actually have
					// to copy the literal, in the case of a mutable object like a
					// list or map, to ensure that if the same code executes again,
					// we get a new, unique object.
					if (rhsA is ValList) {
						return ((ValList)rhsA).EvalCopy(context);
					} else if (rhsA is ValMap) {
						return ((ValMap)rhsA).EvalCopy(context);
					} else if (rhsA == null) {
						return null;
					} else {
						return rhsA.Val(context);
					}
				}

				Value opA = rhsA!=null ? rhsA.Val(context) : null;
				Value opB = rhsB!=null ? rhsB.Val(context) : null;

				if (op == Op.ElemBofA && opB is ValString) {
					// You can now look for a string in almost anything...
					// and we have a convenient (and relatively fast) method for it:
					return ValSeqElem.Resolve(opA, ((ValString)opB).value, context);
				}

				if (op == Op.AEqualB && (opA == null || opB == null)) {
					return ValNumber.Truth(opA == opB);
				}
				if (op == Op.ANotEqualB && (opA == null || opB == null)) {
					return ValNumber.Truth(opA != opB);
				}

				if (opA is ValNumber) {
					double fA = ((ValNumber)opA).value;
					switch (op) {
					case Op.GotoA:
						context.lineNum = (int)fA;
						return null;
					case Op.GotoAifB:
						if (opB != null && opB.BoolValue()) context.lineNum = (int)fA;
						return null;
					case Op.GotoAifTrulyB:
						{
							// Unlike GotoAifB, which branches if B has any nonzero
							// value (including 0.5 or 0.001), this branches only if
							// B is TRULY true, i.e., its integer value is nonzero.
							// (Used for short-circuit evaluation of "or".)
							int i = 0;
							if (opB != null) i = opB.IntValue();
							if (i != 0) context.lineNum = (int)fA;
							return null;
						}
					case Op.GotoAifNotB:
						if (opB == null || !opB.BoolValue()) context.lineNum = (int)fA;
						return null;
					case Op.CallIntrinsicA:
						// NOTE: intrinsics do not go through NextFunctionContext.  Instead
						// they execute directly in the current context.  (But usually, the
						// current context is a wrapper function that was invoked via
						// Op.CallFunction, so it got a parameter context at that time.)
						Intrinsic.Result result = Intrinsic.Execute((int)fA, context, context.partialResult);
						if (result.done) {
							context.partialResult = null;
							return result.result;
						}
						// OK, this intrinsic function is not yet done with its work.
						// We need to stay on this same line and call it again with 
						// the partial result, until it reports that its job is complete.
						context.partialResult = result;
						context.lineNum--;
						return null;
					case Op.NotA:
						return new ValNumber(1.0 - AbsClamp01(fA));
					}
					if (opB is ValNumber || opB == null) {
						double fB = opB != null ? ((ValNumber)opB).value : 0;
						switch (op) {
						case Op.APlusB:
							return new ValNumber(fA + fB);
						case Op.AMinusB:
							return new ValNumber(fA - fB);
						case Op.ATimesB:
							return new ValNumber(fA * fB);
						case Op.ADividedByB:
							return new ValNumber(fA / fB);
						case Op.AModB:
							return new ValNumber(fA % fB);
						case Op.APowB:
							return new ValNumber(Math.Pow(fA, fB));
						case Op.AEqualB:
							return ValNumber.Truth(fA == fB);
						case Op.ANotEqualB:
							return ValNumber.Truth(fA != fB);
						case Op.AGreaterThanB:
							return ValNumber.Truth(fA > fB);
						case Op.AGreatOrEqualB:
							return ValNumber.Truth(fA >= fB);
						case Op.ALessThanB:
							return ValNumber.Truth(fA < fB);
						case Op.ALessOrEqualB:
							return ValNumber.Truth(fA <= fB);
						case Op.AAndB:
							if (!(opB is ValNumber)) fB = opB != null && opB.BoolValue() ? 1 : 0;
							return new ValNumber(Clamp01(fA * fB));
						case Op.AOrB:
							if (!(opB is ValNumber)) fB = opB != null && opB.BoolValue() ? 1 : 0;
							return new ValNumber(1.0 - (1.0 - AbsClamp01(fA)) * (1.0 - AbsClamp01(fB)));
						default:
							break;
						}
					} else if (opB is ValString) {
						// number (op) string
						string sA = opA.ToString();
						string sB = opB.ToString();
						switch (op) {
						case Op.APlusB:
							return new ValString(sA + sB);
						default:
							break;
						}
					}

				} else if (opA is ValString) {
					string sA = ((ValString)opA).value;
					switch (op) {
					case Op.APlusB:
						if (opB == null) return opA;
						return new ValString(sA + opB.ToString());
					case Op.ATimesB:
						{
							double factor = ((ValNumber)opB).value;
							int repeats = (int)factor;
							string result = null;
							for (int i = 0; i < repeats; i++) result += sA;
							int extraChars = (int)(sA.Length * (factor - repeats));
							if (extraChars > 0) result += sA.Substring(0, extraChars);
							return new ValString(result);
						}
					case Op.ADividedByB:
						{
							int repeats = (int)(1.0 / ((ValNumber)opB).value);
							string result = null;
							for (int i = 0; i < repeats; i++) result += sA;
							return new ValString(result);
						}
					case Op.AEqualB:
						return ValNumber.Truth(string.Compare(sA, opB.ToString()) == 0);
					case Op.ANotEqualB:
						return ValNumber.Truth(string.Compare(sA, opB.ToString()) != 0);
					case Op.AGreaterThanB:
						return ValNumber.Truth(string.Compare(sA, opB.ToString()) > 0);
					case Op.AGreatOrEqualB:
						return ValNumber.Truth(string.Compare(sA, opB.ToString()) >= 0);
					case Op.ALessThanB:
						return ValNumber.Truth(string.Compare(sA, opB.ToString()) < 0);
					case Op.ALessOrEqualB:
						return ValNumber.Truth(string.Compare(sA, opB.ToString()) <= 0);
					case Op.ElemBofA:
					case Op.ElemBofIterA:
						{
							int idx = opB.IntValue();
							Check.Range(idx, -sA.Length, sA.Length - 1, "string index");
							if (idx < 0) idx += sA.Length;
							return new ValString(sA.Substring(idx, 1));
						}
					case Op.LengthOfA:
						return new ValNumber(sA.Length);
					default:
						break;
					}
				} else if (opA is ValList) {
					List<Value> list = ((ValList)opA).values;
					if (op == Op.ElemBofA || op == Op.ElemBofIterA) {
						// list indexing
						int idx = opB.IntValue();
						Check.Range(idx, -list.Count, list.Count - 1, "list index");
						if (idx < 0) idx += list.Count;
						return list[idx];
					} else if (op == Op.LengthOfA) {
						return new ValNumber(list.Count);
					} else if (op == Op.APlusB) {
						// list concatenation
						CheckType(opB, typeof(ValList), "list concatenation");
						List<Value> list2 = ((ValList)opB).values;
						List<Value> result = new List<Value>(list.Count + list2.Count);
						foreach (Value v in list) result.Add(v == null ? null : v.Val(context));
						foreach (Value v in list2) result.Add(v == null ? null : v.Val(context));
						return new ValList(result);
					} else if (op == Op.ATimesB || op == Op.ADividedByB) {
						// list replication (or division)
						CheckType(opB, typeof(ValNumber), "list replication");
						double factor = ((ValNumber)opB).value;
						int finalCount = (int)(list.Count * factor);
						List<Value> result = new List<Value>(finalCount);
						for (int i = 0; i < finalCount; i++) {
							result.Add(list[i % list.Count]);
						}
						return new ValList(result);
					}
				} else if (opA is ValMap) {
					if (op == Op.ElemBofA) {
						// map lookup
						// (note, cases where opB is a string are handled above, along with
						// all the other types; so we'll only get here for non-string cases)
						ValSeqElem se = new ValSeqElem(opA, opB);
						return se.Val(context);
						// (This ensures we walk the "__isa" chain in the standard way.)
					} else if (op == Op.ElemBofIterA) {
						// With a map, ElemBofIterA is different from ElemBofA.  This one
						// returns a mini-map containing a key/value pair.
						return ((ValMap)opA).GetKeyValuePair(opB.IntValue());
					} else if (op == Op.LengthOfA) {
						return new ValNumber(((ValMap)opA).Count);
					} else if (op == Op.APlusB) {
						// map combination
						Dictionary<Value, Value> map = ((ValMap)opA).map;
						CheckType(opB, typeof(ValMap), "map combination");
						Dictionary<Value, Value> map2 = ((ValMap)opB).map;
						ValMap result = new ValMap();
						foreach (KeyValuePair<Value, Value> kv in map) result.map[kv.Key] = kv.Value.Val(context);
						foreach (KeyValuePair<Value, Value> kv in map2) result.map[kv.Key] = kv.Value.Val(context);
						return result;
					}
				} else if (opA is ValFunction && opB is ValFunction) {
					Function fA = ((ValFunction)opA).function;
					Function fB = ((ValFunction)opB).function;
					switch (op) {
					case Op.AEqualB:
						return ValNumber.Truth(fA == fB);
					case Op.ANotEqualB:
						return ValNumber.Truth(fA != fB);
					}
				}

				if (op == Op.AAndB || op == Op.AOrB) {
					// We already handled the case where opA was a number above;
					// this code handles the case where opA is something else.
					double fA = opA.BoolValue() ? 1 : 0;
					double fB;
					if (opB is ValNumber) fB = ((ValNumber)opB).value;
					else fB = opB != null && opB.BoolValue() ? 1 : 0;
					double result;
					if (op == Op.AAndB) {
						result = fA * fB;
					} else {
						result = 1.0 - (1.0 - AbsClamp01(fA)) * (1.0 - AbsClamp01(fB));
					}
					return new ValNumber(result);
				}
				return null;
			}

			static double Clamp01(double d) {
				if (d < 0) return 0;
				if (d > 1) return 1;
				return d;
			}
			static double AbsClamp01(double d) {
				if (d < 0) d = -d;
				if (d > 1) return 1;
				return d;
			}

		}

		public class Context {
			public List<Line> code;			// TAC lines we're executing
			public int lineNum;				// next line to be executed
			public ValMap variables;
			public Stack<Value> args;		// pushed arguments for upcoming calls
			public Context parent;			// parent (calling) context
			public Value resultStorage;		// where to store the return value (in the calling context)
			public Machine vm;				// virtual machine
			public Intrinsic.Result partialResult;	// work-in-progress of our current intrinsic
			public int implicitResultCounter;	// how many times we have stored an implicit result

			public bool done {
				get { return lineNum >= code.Count; }
			}

			public Context root {
				get {
					Context c = this;
					while (c.parent != null) c = c.parent;
					return c;
				}
			}

			public Interpreter interpreter {
				get {
					if (vm == null || vm.interpreter == null) return null;
					return vm.interpreter.Target as Interpreter;
				}
			}

			List<Value> temps;			// values of temporaries; temps[0] is always return value

			public Context(List<Line> code) {
				this.code = code;
			}

			public void Reset() {
				lineNum = 0;
				variables = new ValMap();
				temps = null;
			}

			public void JumpToEnd() {
				lineNum = code.Count;
			}

			public void SetTemp(int tempNum, Value value) {
				if (temps == null) temps = new List<Value>();
				while (temps.Count <= tempNum) temps.Add(null);
				temps[tempNum] = value;
			}

			public Value GetTemp(int tempNum) {
				return temps[tempNum];
			}

			public Value GetTemp(int tempNum, Value defaultValue) {
				if (temps != null && tempNum < temps.Count) return temps[tempNum];
				return defaultValue;
			}

			public void SetVar(string identifier, Value value) {
				if (identifier == "globals" || identifier == "locals") {
					throw new RuntimeException("can't assign to " + identifier);
				}
				if (variables == null) variables = new ValMap();
				variables[identifier] = value;
			}

			public Value GetVar(string identifier) {
				if (identifier == "locals") {
					if (variables == null) variables = new ValMap();
					return variables;
				}
				if (identifier == "globals") {
					if (root.variables == null) root.variables = new ValMap();
					return root.variables;
				}
				if (variables != null && variables.ContainsKey(identifier)) {
					return variables[identifier];
				}

				// OK, we don't have a local variable with that name.
				// Check higher scopes.
				Context c = parent;
				while (c != null) {
					if (c.variables != null && c.variables.ContainsKey(identifier)) {
						return c.variables[identifier];
					}
					c = c.parent;
				}

				// Finally, check intrinsics.
				Intrinsic intrinsic = Intrinsic.GetByName(identifier);
				if (intrinsic != null) return intrinsic.GetFunc();

				// No luck there either?  Undefined identifier.
				throw new UndefinedIdentifierException(identifier);
			}

			public void StoreValue(Value lhs, Value value) {
				if (lhs is ValTemp) {
					SetTemp(((ValTemp)lhs).tempNum, value);
				} else if (lhs is ValVar) {
					SetVar(((ValVar)lhs).identifier, value);
				} else if (lhs is ValSeqElem) {
					ValSeqElem seqElem = (ValSeqElem)lhs;
					Value seq = seqElem.sequence.Val(this);
					if (seq == null) throw new RuntimeException("can't set indexed element of null");
					if (!seq.CanSetElem()) throw new RuntimeException("can't set an indexed element in this type");
					Value index = seqElem.index;
					if (index is ValVar || index is ValSeqElem) index = index.Val(this);
					seq.SetElem(index, value);
				} else {
					if (lhs != null) throw new RuntimeException("not an lvalue");
				}
			}

			/// <summary>
			/// Store a parameter argument in preparation for an upcoming call
			/// (which should be executed in the context returned by NextCallContext).
			/// </summary>
			/// <param name="arg">Argument.</param>
			public void PushParamArgument(Value arg) {
				if (args == null) args = new Stack<Value>();
				args.Push(arg);
			}

			/// <summary>
			/// Get a context for the next call, which includes any parameter arguments
			/// that have been set.
			/// </summary>
			/// <returns>The call context.</returns>
			/// <param name="func">Function to call.</param>
			/// <param name="argCount">How many arguments to pop off the stack.</param>
			/// <param name="gotSelf">Whether this method was called with dot syntax.</param> 
			/// <param name="resultStorage">Value to stuff the result into when done.</param>
			public Context NextCallContext(Function func, int argCount, bool gotSelf, Value resultStorage) {
				Context result = new Context(func.code);

				result.code = func.code;
				result.resultStorage = resultStorage;
				result.parent = this;
				result.vm = vm;

				// Stuff arguments, stored in our 'args' stack,
				// into local variables corrersponding to parameter names.
				// As a special case, skip over the first parameter if it is named 'self'
				// and we were invoked with dot syntax.
				int selfParam = (gotSelf && func.parameters.Count > 0 && func.parameters[0].name == "self" ? 1 : 0);
				for (int i = 0; i < argCount; i++) {
					// Careful -- when we pop them off, they're in reverse order.
					Value argument = args.Pop();
					int paramNum = argCount - 1 - i + selfParam;
					if (paramNum >= func.parameters.Count) {
						throw new TooManyArgumentsException();
					}
					result.SetVar(func.parameters[paramNum].name, argument);
				}
				// And fill in the rest with default values
				for (int paramNum = argCount+selfParam; paramNum < func.parameters.Count; paramNum++) {
					result.SetVar(func.parameters[paramNum].name, func.parameters[paramNum].defaultValue);
				}

				return result;
			}

			public void Dump() {
				Debug.WriteLine("CODE:");
				for (int i = 0; i < code.Count; i++) {
					Debug.WriteLine("{0} {1:00}: {2}", i == lineNum ? ">" : " ", i, code[i]);
				}

				Debug.WriteLine("\nVARS:");
				if (variables == null) {
					Debug.WriteLine(" NONE");
				} else {
					foreach (Value v in variables.Keys) {
						string id = v.ToString();
						Debug.WriteLine(string.Format("{0}: {1}", id, variables[id]));
					}
				}

				Debug.WriteLine("\nTEMPS:");
				if (temps == null) {
					Debug.WriteLine(" NONE");
				} else {
					for (int i = 0; i < temps.Count; i++) {
						Debug.WriteLine(string.Format("_{0}: {1}", i, temps[i]));
					}
				}
			}

			public override string ToString() {
				return string.Format("Context[{0}/{1}]", lineNum, code.Count);
			}
		}

		public class Machine {
			public System.WeakReference interpreter;		// interpreter hosting this machine
			public TextOutputMethod standardOutput;	// where print() results should go
			public bool storeImplicit = false;		// whether to store implicit values (e.g. for REPL)
			public Context globalContext {			// contains global variables
				get { return _globalContext; }
			}

			public bool done {
				get { return (stack.Count <= 1 && stack.Peek().done); }
			}

			public double runTime {
				get { return stopwatch == null ? 0 : stopwatch.Elapsed.TotalSeconds; }
			}

			Context _globalContext;
			Stack<Context> stack;
			Stopwatch stopwatch;

			public Machine(Context globalContext, TextOutputMethod standardOutput) {
				_globalContext = globalContext;
				_globalContext.vm = this;
                this.standardOutput = (standardOutput == null ? delegate (string text) { Debug.WriteLine(text); } : standardOutput);
				stack = new Stack<Context>();
				stack.Push(_globalContext);
			}
			
			public void Stop() {
				while (stack.Count > 1) stack.Pop();
				stack.Peek().JumpToEnd();
			}
			
			public void Reset() {
				while (stack.Count > 1) stack.Pop();
				stack.Peek().Reset();
			}

			public void Step() {
				if (stack.Count == 0) return;		// not even a global context
				if (stopwatch == null) {
					stopwatch = new System.Diagnostics.Stopwatch();
					stopwatch.Start();
				}
				Context context = stack.Peek();
				while (context.done) {
					if (stack.Count == 1) return;	// all done (can't pop the global context)
					PopContext();
					context = stack.Peek();
				}

				Line line = context.code[context.lineNum++];
				try {
					DoOneLine(line, context);
				} catch (MiniscriptException mse) {
					mse.location = line.location;
					throw mse;
				}
			}

			void DoOneLine(Line line, Context context) {
//				Console.WriteLine("EXECUTING line " + (context.lineNum-1) + ": " + line);
				if (line.op == Line.Op.PushParam) {
					Value val = line.rhsA == null ? null : line.rhsA.Val(context);
					context.PushParamArgument(val);
				} else if (line.op == Line.Op.CallFunctionA) {
					// Resolve rhsA.  If it's a function, invoke it; otherwise,
					// just store it directly.
					Value funcVal = line.rhsA.Val(context);		// resolves the whole dot chain, if any
					if (funcVal is ValFunction) {
						Value self = null;
						if (line.rhsA is ValSeqElem) {
							self = ((ValSeqElem)(line.rhsA)).sequence.Val(context);
						}
						ValFunction func = (ValFunction)funcVal;
						int argCount = line.rhsB.IntValue();
						Context nextContext = context.NextCallContext(func.function, argCount, self != null, line.lhs);
						if (self != null) nextContext.SetVar("self", self);
						stack.Push(nextContext);
					} else {
						context.StoreValue(line.lhs, funcVal);
					}
				} else if (line.op == Line.Op.ReturnA) {
					Value val = line.Evaluate(context);
					context.StoreValue(line.lhs, val);
					PopContext();
				} else if (line.op == Line.Op.AssignImplicit) {
					Value val = line.Evaluate(context);
					if (storeImplicit) {
						context.StoreValue(ValVar.implicitResult, val);
						context.implicitResultCounter++;
					}
				} else {
					Value val = line.Evaluate(context);
					context.StoreValue(line.lhs, val);
				}
			}

			void PopContext() {
				// Our top context is done; pop it off, and copy the return value in temp 0.
				if (stack.Count == 1) return;	// down to just the global stack (which we keep)
				Context context = stack.Pop();
				Value result = context.GetTemp(0, null);
				Value storage = context.resultStorage;
				context = stack.Peek();
				context.StoreValue(storage, result);
//				if (storage is ValTemp) context.SetTemp(((ValTemp)storage).tempNum, result);
//				else if (storage is ValVar) context.SetVar(((ValVar)storage).identifier, result);
//				else if (storage is ValSeqElem) ((ValSeqElem)storage).SetElem(
//				else throw new RuntimeException();	// (should always be ValTemp or ValVar)
			}

			public Context GetTopContext() {
				return stack.Peek();
			}

			public void DumpTopContext() {
				stack.Peek().Dump();
			}
		}

		public static void Dump(List<Line> lines) {
			int lineNum = 0;
			foreach (Line line in lines) {
				Debug.WriteLine((lineNum++).ToString() + ". " + line);
			}
		}

		public static ValTemp LTemp(int tempNum) {
			return new ValTemp(tempNum);
		}
		public static ValVar LVar(string identifier) {
			return new ValVar(identifier);
		}
		public static ValTemp RTemp(int tempNum) {
			return new ValTemp(tempNum);
		}
		public static ValNumber Num(double value) {
			return new ValNumber(value);
		}
		public static ValString Str(string value) {
			return new ValString(value);
		}
		public static ValNumber IntrinsicByName(string name) {
			return new ValNumber(Intrinsic.GetByName(name).id);
		}

		public static void CheckType(Value val, System.Type requiredType, string desc=null) {
			// if (!requiredType.IsInstanceOfType(val)) {
            if (requiredType != val.GetType()) {
				throw new TypeException(string.Format("got a {0} where a {1} was required{2}",
					val.GetType(), requiredType, desc == null ? null : " (" + desc + ")"));
			}

		}

	}
}
