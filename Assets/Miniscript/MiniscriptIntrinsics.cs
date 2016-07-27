using System;
using System.Collections.Generic;

namespace Miniscript {
	public delegate Intrinsic.Result IntrinsicCode(TAC.Context context, Intrinsic.Result partialResult);

	public class Intrinsic {
		public string name;
		public IntrinsicCode code;

		public int id { get { return numericID; } }

		private Function function;
		private ValFunction valFunction;	// (cached wrapper for function)
		int numericID;		// also its index in the 'all' list

		static List<Intrinsic> all = new List<Intrinsic>() { null };
		static Dictionary<string, Intrinsic> nameMap = new Dictionary<string, Intrinsic>();

		public static Intrinsic Create(string name) {
			Intrinsic result = new Intrinsic();
			result.name = name;
			result.numericID = all.Count;
			result.function = new Function(null);
			result.valFunction = new ValFunction(result.function);
			all.Add(result);
			nameMap[name] = result;
			return result;
		}

		public static Intrinsic GetByID(int id) {
			return all[id];
		}

		public static Intrinsic GetByName(string name) {
			Intrinsics.InitIfNeeded();
			Intrinsic result = null;
			if (nameMap.TryGetValue(name, out result)) return result;
			return null;
		}

		public void AddParam(string name, Value defaultValue=null) {
			function.parameters.Add(new Function.Param(name, defaultValue));
		}

		public void AddParam(string name, double defaultValue) {
			Value defVal;
			if (defaultValue == 0) defVal = ValNumber.zero;
			else if (defaultValue == 1) defVal = ValNumber.one;
			else defVal = TAC.Num(defaultValue);
			function.parameters.Add(new Function.Param(name, defVal));
		}

		public ValFunction GetFunc() {
			if (function.code == null) {
				// Our little wrapper function is a single opcode: CallIntrinsicA.
				// It really exists only to provide a local variable context for the parameters.
				function.code = new List<TAC.Line>();
				function.code.Add(new TAC.Line(TAC.LTemp(0), TAC.Line.Op.CallIntrinsicA, TAC.Num(numericID)));
			}
			return valFunction;
		}

		public static Result Execute(int id, TAC.Context context, Result partialResult) {
			Intrinsic item = GetByID(id);
			return item.code(context, partialResult);
		}

		public class Result {
			public bool done;		// true if our work is complete; false if we need to Continue
			public Value result;	// final result if done; in-progress data if not done

			public Result(Value result, bool done=true) {
				this.done = done;
				this.result = result;
			}

			public Result(double resultNum) {
				this.done = true;
				this.result = new ValNumber(resultNum);
			}

			public Result(string resultStr) {
				this.done = true;
				this.result = new ValString(resultStr);
			}

			static Result _null = new Result(null, true);
			public static Result Null { get { return _null; } }
			
			static Result _true = new Result(ValNumber.one, true);
			public static Result True { get { return _true; } }
			
			static Result _false = new Result(ValNumber.zero, true);
			public static Result False { get { return _false; } }
			
			static Result _waiting = new Result(null, false);
			public static Result Waiting { get { return _waiting; } }
		}
	}

	public static class Intrinsics {

		static bool initialized;
		public static void InitIfNeeded() {
			if (initialized) return;
			initialized = true;
			Intrinsic f;

			// abs(x)
			f = Intrinsic.Create("abs");
			f.AddParam("x", 0);
			f.code = (context, partialResult) => {
				return new Intrinsic.Result(Math.Abs(context.GetVar("x").DoubleValue()));
			};

			// acos(x)
			f = Intrinsic.Create("acos");
			f.AddParam("x", 0);
			f.code = (context, partialResult) => {
				return new Intrinsic.Result(Math.Acos(context.GetVar("x").DoubleValue()));
			};

			// asin(x)
			f = Intrinsic.Create("asin");
			f.AddParam("x", 0);
			f.code = (context, partialResult) => {
				return new Intrinsic.Result(Math.Asin(context.GetVar("x").DoubleValue()));
			};

			// atan(x)
			f = Intrinsic.Create("atan");
			f.AddParam("x", 0);
			f.code = (context, partialResult) => {
				return new Intrinsic.Result(Math.Atan(context.GetVar("x").DoubleValue()));
			};
			
			// char(i)
			f = Intrinsic.Create("char");
			f.AddParam("codePoint", 65);
			f.code = (context, partialResult) => {
				int codepoint = context.GetVar("codePoint").IntValue();
				string s = char.ConvertFromUtf32(codepoint);
				return new Intrinsic.Result(s);
			};
			
			// code(s)
			f = Intrinsic.Create("code");
			f.AddParam("self");
			f.code = (context, partialResult) => {
				Value self = context.GetVar("self");
				int codepoint = 0;
				if (self != null) codepoint = char.ConvertToUtf32(self.ToString(), 0);
				return new Intrinsic.Result(codepoint);
			};
						
			// cos(radians)
			f = Intrinsic.Create("cos");
			f.AddParam("radians", 0);
			f.code = (context, partialResult) => {
				return new Intrinsic.Result(Math.Cos(context.GetVar("radians").DoubleValue()));
			};

			// hash
			f = Intrinsic.Create("hash");
			f.AddParam("obj");
			f.code = (context, partialResult) => {
				Value val = context.GetVar("obj");
				return new Intrinsic.Result(val.Hash());
			};

			// hasIndex
			f = Intrinsic.Create("hasIndex");
			f.AddParam("self");
			f.AddParam("index");
			f.code = (context, partialResult) => {
				Value self = context.GetVar("self");
				Value index = context.GetVar("index");
				if (self is ValList) {
					List<Value> list = ((ValList)self).values;
					int i = index.IntValue();
					return new Intrinsic.Result(i >= -list.Count && i < list.Count ? 1 : 0);
				} else if (self is ValString) {
					string str = ((ValString)self).value;
					int i = index.IntValue();
					return new Intrinsic.Result(i >= -str.Length && i < str.Length ? 1 : 0);
				} else if (self is ValMap) {
					ValMap map = (ValMap)self;
					return new Intrinsic.Result(map.ContainsKey(index) ? 1 : 0);
				}
				return Intrinsic.Result.Null;
			};
			
			// indexes
			//	Returns the keys of a dictionary, or the indexes for a string or list.
			f = Intrinsic.Create("indexes");
			f.AddParam("self");
			f.code = (context, partialResult) => {
				Value self = context.GetVar("self");
				if (self is ValMap) {
					ValMap map = (ValMap)self;
					List<Value> values = new List<Value>(map.map.Keys);
					return new Intrinsic.Result(new ValList(values));
				} else if (self is ValString) {
					string str = ((ValString)self).value;
					List<Value> values = new List<Value>(str.Length);
					for (int i=0; i<str.Length; i++) {
						values.Add(TAC.Num(i));
					}
					return new Intrinsic.Result(new ValList(values));
				} else if (self is ValList) {
					List<Value> list = ((ValList)self).values;
					List<Value> values = new List<Value>(list.Count);
					for (int i=0; i<list.Count; i++) {
						values.Add(TAC.Num(i));
					}
					return new Intrinsic.Result(new ValList(values));
				}
				return Intrinsic.Result.Null;
			};
			
			// indexOf
			//	Returns index or key of the given value, or if not found, returns null.
			f = Intrinsic.Create("indexOf");
			f.AddParam("self");
			f.AddParam("value");
			f.code = (context, partialResult) => {
				Value self = context.GetVar("self");
				Value value = context.GetVar("value");
				if (self is ValList) {
					List<Value> list = ((ValList)self).values;
					int idx = list.FindIndex(x => x.Equals(value));
					if (idx >= 0) return new Intrinsic.Result(idx);
				} else if (self is ValString) {
					string str = ((ValString)self).value;
					string s = value.ToString();
					int idx = str.IndexOf(s);
					if (idx >= 0) return new Intrinsic.Result(idx);
				} else if (self is ValMap) {
					ValMap map = (ValMap)self;
					foreach (Value k in map.map.Keys) {
						if (map.map[k].Equals(value)) return new Intrinsic.Result(k);
					}
				}
				return Intrinsic.Result.Null;
			};

			// self.len
			f = Intrinsic.Create("len");
			f.AddParam("self");
			f.code = (context, partialResult) => {
				Value val = context.GetVar("self");
				if (val is ValList) {
					List<Value> list = ((ValList)val).values;
					return new Intrinsic.Result(list.Count);
				} else if (val is ValString) {
					string str = ((ValString)val).value;
					return new Intrinsic.Result(str.Length);
				} else if (val is ValMap) {
					return new Intrinsic.Result(((ValMap)val).Count);
				}
				return Intrinsic.Result.Null;
			};
			
			// s.lower
			f = Intrinsic.Create("lower");
			f.AddParam("self");
			f.code = (context, partialResult) => {
				Value val = context.GetVar("self");
				if (val is ValString) {
					string str = ((ValString)val).value;
					return new Intrinsic.Result(str.ToLower());
				}
				return new Intrinsic.Result(val);
			};

			// pi
			f = Intrinsic.Create("pi");
			f.code = (context, partialResult) => {
				return new Intrinsic.Result(Math.PI);
			};

			// print(s)
			f = Intrinsic.Create("print");
			f.AddParam("s", ValString.empty);
			f.code = (context, partialResult) => {
				Value s = context.GetVar("s");
				if (s != null) context.vm.standardOutput(s.ToString());
				else context.vm.standardOutput("null");
				return Intrinsic.Result.Null;
			};

			// range(from, to, step)
			f = Intrinsic.Create("range");
			f.AddParam("from", 0);
			f.AddParam("to", 0);
			f.AddParam("step");
			f.code = (context, partialResult) => {
				Value p0 = context.GetVar("from");
				Value p1 = context.GetVar("to");
				Value p2 = context.GetVar("step");
				if (!(p0 is ValNumber) || !(p1 is ValNumber)) {
					throw new RuntimeException("range requires 2 or 3 numeric arguments");
				}
				double fromVal = (p0 as ValNumber).value;
				double toVal = (p1 as ValNumber).value;
				double step = (toVal >= fromVal ? 1 : -1);
				if (p2 is ValNumber) step = (p2 as ValNumber).value;
				List<Value> values = new List<Value>();
				for (double v = fromVal; step > 0 ? (v <= toVal) : (v >= toVal); v += step) {
					values.Add(TAC.Num(v));
				}
				return new Intrinsic.Result(new ValList(values));
			};

			// remove(self, key or index or substring)
			// 		list: mutated in place, returns null, error if index out of range
			//		map: mutated in place; returns 1 if key found, 0 otherwise
			//		string: returns new string with first occurrence of k removed
			f = Intrinsic.Create("remove");
			f.AddParam("self");
			f.AddParam("k");
			f.code = (context, partialResult) => {
				Value self = context.GetVar("self");
				Value k = context.GetVar("k");
				if (self == null || k == null) throw new RuntimeException("argument to 'remove' must not be null");
				if (self is ValMap) {
					ValMap selfMap = (ValMap)self;
					if (selfMap.map.ContainsKey(k)) {
						selfMap.map.Remove(k);
						return new Intrinsic.Result(ValNumber.one);
					}
					return new Intrinsic.Result(ValNumber.zero);
				} else if (self is ValList) {
					ValList selfList = (ValList)self;
					int idx = k.IntValue();
					if (idx < 0) idx += selfList.values.Count;
					Check.Range(idx, 0, selfList.values.Count-1);
					selfList.values.RemoveAt(idx);
					return Intrinsic.Result.Null;
				} else if (self is ValString) {
					ValString selfStr = (ValString)self;
					string substr = k.ToString();
					int foundPos = selfStr.value.IndexOf(substr);
					if (foundPos < 0) return new Intrinsic.Result(self);
					return new Intrinsic.Result(selfStr.value.Remove(foundPos, substr.Length));
				}
				throw new TypeException("Type Error: 'remove' requires map, list, or string");
			};

			// round(x, decimalPlaces)
			f = Intrinsic.Create("round");
			f.AddParam("x", 0);
			f.AddParam("decimalPlaces", 0);
			f.code = (context, partialResult) => {
				double num = context.GetVar("x").DoubleValue();
				int decimalPlaces = context.GetVar("decimalPlaces").IntValue();
				return new Intrinsic.Result(Math.Round(num, decimalPlaces));
			};


			// rnd(seed)
			f = Intrinsic.Create("rnd");
			f.AddParam("seed");
			f.code = (context, partialResult) => {
				if (random == null) random = new Random();
				Value seed = context.GetVar("seed");
				if (seed != null) random = new Random(seed.IntValue());
				return new Intrinsic.Result(random.NextDouble());
			};

			// sign(x)
			f = Intrinsic.Create("sign");
			f.AddParam("x", 0);
			f.code = (context, partialResult) => {
				return new Intrinsic.Result(Math.Sign(context.GetVar("x").DoubleValue()));
			};

			// sin(radians)
			f = Intrinsic.Create("sin");
			f.AddParam("radians", 0);
			f.code = (context, partialResult) => {
				return new Intrinsic.Result(Math.Sin(context.GetVar("radians").DoubleValue()));
			};
				
			// slice(seq, from, to)
			f = Intrinsic.Create("slice");
			f.AddParam("seq");
			f.AddParam("from", 0);
			f.AddParam("to", -1);
			f.code = (context, partialResult) => {
				Value seq = context.GetVar("seq");
				int fromIdx = context.GetVar("from").IntValue();
				int toIdx = context.GetVar("to").IntValue();
				if (seq is ValList) {
					List<Value> list = ((ValList)seq).values;
					if (fromIdx < 0) fromIdx += list.Count;
					if (fromIdx < 0) fromIdx = 0;
					if (toIdx <= 0) toIdx += list.Count;
					if (toIdx > list.Count) toIdx = list.Count;
					ValList slice = new ValList();
					if (fromIdx < list.Count && toIdx > fromIdx) {
						for (int i = fromIdx; i < toIdx; i++) {
							slice.values.Add(list[i]);
						}
					}
					return new Intrinsic.Result(slice);
				} else if (seq is ValString) {
					string str = ((ValString)seq).value;
					if (fromIdx < 0) fromIdx += str.Length;
					if (fromIdx < 0) fromIdx = 0;
					if (toIdx <= 0) toIdx += str.Length;
					if (toIdx > str.Length) toIdx = str.Length;
					return new Intrinsic.Result(str.Substring(fromIdx, toIdx - fromIdx));
				}
				return Intrinsic.Result.Null;
			};

			// sqrt(x)
			f = Intrinsic.Create("sqrt");
			f.AddParam("x", 0);
			f.code = (context, partialResult) => {
				return new Intrinsic.Result(Math.Sqrt(context.GetVar("x").DoubleValue()));
			};

			// str(x)
			f = Intrinsic.Create("str");
			f.AddParam("x", ValString.empty);
			f.code = (context, partialResult) => {
				return new Intrinsic.Result(context.GetVar("x").ToString());
			};

			// shuffle(self)
			f = Intrinsic.Create("shuffle");
			f.AddParam("self");
			f.code = (context, partialResult) => {
				Value self = context.GetVar("self");
				if (random == null) random = new Random();
				if (self is ValList) {
					List<Value> list = ((ValList)self).values;
					// We'll do a Fisher-Yates shuffle, i.e., swap each element
					// with a randomly selected one.
					for (int i=list.Count-1; i >= 1; i--) {
						int j = random.Next(i+1);
						Value temp = list[j];
						list[j] = list[i];
						list[i] = temp;
					}
				} else if (self is ValMap) {
					Dictionary<Value, Value> map = ((ValMap)self).map;
					// Fisher-Yates again, but this time, what we're swapping
					// is the values associated with the keys, not the keys themselves.
					List<Value> keys = System.Linq.Enumerable.ToList(map.Keys);
					for (int i=keys.Count-1; i >= 1; i--) {
						int j = random.Next(i+1);
						Value keyi = keys[i];
						Value keyj = keys[j];
						Value temp = map[keyj];
						map[keyj] = map[keyi];
						map[keyi] = temp;
					}
				}
				return Intrinsic.Result.Null;
			};

			// sum(self)
			f = Intrinsic.Create("sum");
			f.AddParam("self");
			f.code = (context, partialResult) => {
				Value val = context.GetVar("self");
				double sum = 0;
				if (val is ValList) {
					List<Value> list = ((ValList)val).values;
					foreach (Value v in list) {
						sum += v.DoubleValue();
					}
				} else if (val is ValMap) {
					Dictionary<Value, Value> map = ((ValMap)val).map;
					foreach (Value v in map.Values) {
						sum += v.DoubleValue();
					}
				}
				return new Intrinsic.Result(sum);
			};

			// tan(radians)
			f = Intrinsic.Create("tan");
			f.AddParam("radians", 0);
			f.code = (context, partialResult) => {
				return new Intrinsic.Result(Math.Tan(context.GetVar("radians").DoubleValue()));
			};

			// time
			f = Intrinsic.Create("time");
			f.code = (context, partialResult) => {
				return new Intrinsic.Result(context.vm.runTime);
			};
			
			// s.lower
			f = Intrinsic.Create("upper");
			f.AddParam("self");
			f.code = (context, partialResult) => {
				Value val = context.GetVar("self");
				if (val is ValString) {
					string str = ((ValString)val).value;
					return new Intrinsic.Result(str.ToUpper());
				}
				return new Intrinsic.Result(val);
			};
			
			// val(s)
			f = Intrinsic.Create("val");
			f.AddParam("self", new ValString("0"));
			f.code = (context, partialResult) => {
				Value val = context.GetVar("self");
				if (val is ValNumber) return new Intrinsic.Result(val);
				if (val is ValString) {
					double value = 0;
					double.TryParse(val.ToString(), out value);
					return new Intrinsic.Result(value);
				}
				return Intrinsic.Result.Null;
			};

			// wait(seconds)
			f = Intrinsic.Create("wait");
			f.AddParam("seconds", 1);
			f.code = (context, partialResult) => {
				double now = context.vm.runTime;
				if (partialResult == null) {
					// Just starting our wait; calculate end time and return as partial result
					double interval = context.GetVar("seconds").DoubleValue();
					return new Intrinsic.Result(new ValNumber(now + interval), false);
				} else {
					// Continue until current time exceeds the time in the partial result
					if (now > partialResult.result.DoubleValue()) return Intrinsic.Result.Null;
					return partialResult;
				}
			};

		}

		static Random random;	// TODO: consider storing this on the context, instead of global!


		// Helper method to compile a call to Slice.
		public static void CompileSlice(List<TAC.Line> code, Value list, Value fromIdx, Value toIdx, int resultTempNum) {
			code.Add(new TAC.Line(null, TAC.Line.Op.PushParam, list));
			code.Add(new TAC.Line(null, TAC.Line.Op.PushParam, fromIdx == null ? TAC.Num(0) : fromIdx));
			code.Add(new TAC.Line(null, TAC.Line.Op.PushParam, toIdx == null ? TAC.Num(0) : toIdx));
			ValFunction func = Intrinsic.GetByName("slice").GetFunc();
			code.Add(new TAC.Line(TAC.LTemp(resultTempNum), TAC.Line.Op.CallFunctionA, func, TAC.Num(3)));
		}

		static ValMap _listType = null;
		public static ValMap ListType() {
			if (_listType == null) {
				_listType = new ValMap();
				_listType["hasIndex"] = Intrinsic.GetByName("hasIndex").GetFunc();
				_listType["indexes"] = Intrinsic.GetByName("indexes").GetFunc();
				_listType["indexOf"] = Intrinsic.GetByName("indexOf").GetFunc();
				_listType["len"] = Intrinsic.GetByName("len").GetFunc();
				_listType["shuffle"] = Intrinsic.GetByName("shuffle").GetFunc();
				_listType["sum"] = Intrinsic.GetByName("sum").GetFunc();
				_listType["remove"] = Intrinsic.GetByName("remove").GetFunc();
			}
			return _listType;
		}

		static ValMap _stringType = null;
		public static ValMap StringType() {
			if (_stringType == null) {
				_stringType = new ValMap();
				_stringType["hasIndex"] = Intrinsic.GetByName("hasIndex").GetFunc();
				_stringType["indexes"] = Intrinsic.GetByName("indexes").GetFunc();
				_stringType["indexOf"] = Intrinsic.GetByName("indexOf").GetFunc();
				_stringType["code"] = Intrinsic.GetByName("code").GetFunc();
				_stringType["len"] = Intrinsic.GetByName("len").GetFunc();
				_stringType["lower"] = Intrinsic.GetByName("lower").GetFunc();
				_stringType["val"] = Intrinsic.GetByName("val").GetFunc();
				_stringType["remove"] = Intrinsic.GetByName("remove").GetFunc();
				_stringType["upper"] = Intrinsic.GetByName("upper").GetFunc();
			}
			return _stringType;
		}

		static ValMap _mapType = null;
		public static ValMap MapType() {
			if (_mapType == null) {
				_mapType = new ValMap();
				_mapType["hasIndex"] = Intrinsic.GetByName("hasIndex").GetFunc();
				_mapType["indexes"] = Intrinsic.GetByName("indexes").GetFunc();
				_mapType["indexOf"] = Intrinsic.GetByName("indexOf").GetFunc();
				_mapType["len"] = Intrinsic.GetByName("len").GetFunc();
				_mapType["shuffle"] = Intrinsic.GetByName("shuffle").GetFunc();
				_mapType["sum"] = Intrinsic.GetByName("sum").GetFunc();
				_mapType["remove"] = Intrinsic.GetByName("remove").GetFunc();
			}
			return _mapType;
		}

	}
}

