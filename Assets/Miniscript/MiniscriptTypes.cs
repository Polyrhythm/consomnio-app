using System;
using System.Collections.Generic;
using System.Linq;

namespace Miniscript {

	public abstract class Value {
		public virtual Value Val(TAC.Context context) {
			return this;		// most types evaluate to themselves
		}

		public virtual Value FullEval(TAC.Context context) {
			return this;
		}

		public virtual int IntValue() {
			return (int)DoubleValue();
		}

		public virtual uint UIntValue() {
			return (uint)DoubleValue();
		}
		
		public virtual float FloatValue() {
			return (float)DoubleValue();
		}
		
		public virtual double DoubleValue() {
			Error.Assert(false);	// most types don't have a numeric value
			return 0;
		}

		public virtual bool BoolValue() {
			return IntValue() != 0;
		}

		public virtual string CodeForm(int recursionLimit=-1) {
			return ToString();
		}

		public abstract int Hash();
		public abstract bool Equals(Value rhs);

		public virtual bool CanSetElem() { return false; }
		public virtual void SetElem(Value
			index, Value value) {}

	}

	public class ValNumber : Value {
		public double value;

		public ValNumber(double value) {
			this.value = value;
		}

		public override string ToString() {
			return value.ToString();
		}

		public override int IntValue() {
			return (int)value;
		}

		public override double DoubleValue() {
			return value;
		}

		public override bool BoolValue() {
			return value != 0;
		}

		public override int Hash() {
			return value.GetHashCode();
		}

		public override bool Equals(Value rhs) {
			return rhs is ValNumber && ((ValNumber)rhs).value == value;
		}

		static ValNumber _zero = new ValNumber(0), _one = new ValNumber(1);
		public static ValNumber zero { get { return _zero; } }
		public static ValNumber one { get { return _one; } }
		public static ValNumber Truth(bool truthValue) {
			return truthValue ? one : zero;
		}
	}

	public class ValString : Value {
		public string value;

		public ValString(string value) {
			this.value = value;
		}

		public override string ToString() {
			return value;
		}

		public override string CodeForm(int recursionLimit=-1) {
			return "\"" + value.Replace("\"", "\"\"") + "\"";
		}

		public override bool BoolValue() {
			return !string.IsNullOrEmpty(value);
		}

		public override int Hash() {
			return value.GetHashCode();
		}

		public override bool Equals(Value rhs) {
			return rhs is ValString && ((ValString)rhs).value == value;
		}

		public Value GetElem(Value index) {
			int i = index.IntValue();
			if (i < 0) i += value.Length;
			if (i < 0 || i >= value.Length) {
				throw new IndexException("Index Error (string index " + index + " out of range)");

			}
			return new ValString(value.Substring(i, 1));
		}

		// Magic identifier for the is-a entry in the class system:
		public static ValString magicIsA = new ValString("__isa");

		// Flyweight shared temporary ValString, to be used only for fleeting
		// purposes, such as storing/retrieving from a ValMap.
		static ValString _sharedTemp = new ValString(null);
		public static ValString Temp(string value) {
			_sharedTemp.value = value;
			return _sharedTemp;
		}

		static ValString _empty = new ValString("");
		public static ValString empty { get { return _empty; } }

	}

	public class ValList : Value {
		public List<Value> values;

		public ValList(List<Value> values = null) {
			this.values = values == null ? new List<Value>() : values;
		}

		public override Value FullEval(TAC.Context context) {
			// Evaluate each of our list elements, and if any of those is
			// a variable or temp, then resolve those now.
			// CAUTION: do not mutate our original list!  We may need
			// it in its original form on future iterations.
			ValList result = null;
			for (int i = 0; i < values.Count; i++) {
				bool copied = false;
				if (values[i] is ValTemp || values[i] is ValVar) {
					Value newVal = values[i].Val(context);
					if (newVal != values[i]) {
						// OK, something changed, so we're going to need a new copy of the list.
						if (result == null) {
							result = new ValList();
							for (int j = 0; j < i; j++) result.values.Add(values[j]);
						}
						result.values.Add(newVal);
						copied = true;
					}
				}
				if (!copied && result != null) {
					// No change; but we have new results to return, so copy it as-is
					result.values.Add(values[i]);
				}
			}
			if (result != null) return result;
			return this;
		}

		public ValList EvalCopy(TAC.Context context) {
			// Create a copy of this list, evaluating its members as we go.
			// This is used when a list literal appears in the source, to
			// ensure that each time that code executes, we get a new, distinct
			// mutable object, rather than the same object multiple times.
			ValList result = new ValList();
			for (int i = 0; i < values.Count; i++) {
				result.values.Add(values[i].Val(context));
			}
			return result;
		}

		public override string CodeForm(int recursionLimit=-1) {
			if (recursionLimit == 0) return "[...]";
			string[] strs = new string[values.Count];
			for (int i = 0; i < values.Count; i++) {
				strs[i] = values[i].CodeForm(recursionLimit - 1);
			}
			return "[" + String.Join(", ", strs) + "]";
		}

		public override string ToString() {
			return CodeForm(3);
		}

		public override bool BoolValue() {
			return values != null && values.Count > 0;
		}

		public override int Hash() {
			return values.GetHashCode();
//			int result = values.Count.GetHashCode();
//			for (int i = 0; i < values.Count; i++) {
//				result ^= values[i].Hash();
//			}
//			return result;
		}

		public override bool Equals(Value rhs) {
			if (!(rhs is ValList)) return false;
			List<Value> rhl = ((ValList)rhs).values;
			int count = values.Count;
			if (count != rhl.Count) return false;
			for (int i = 0; i < count; i++) {
				if (!values[i].Equals(rhl[i])) return false;
			}
			return true;
		}

		public override bool CanSetElem() { return true; }

		public override void SetElem(Value index, Value value) {
			int i = index.IntValue();
			if (i < 0) i += values.Count;
			if (i < 0 || i >= values.Count) {
				throw new IndexException("Index Error (list index " + index + " out of range)");
			}
			values[i] = value;
		}

		public Value GetElem(Value index) {
			int i = index.IntValue();
			if (i < 0) i += values.Count;
			if (i < 0 || i >= values.Count) {
				throw new IndexException("Index Error (list index " + index + " out of range)");

			}
			return values[i];
		}

	}

	public class ValMap : Value {
		public Dictionary<Value, Value> map;

		public ValMap() {
			this.map = new Dictionary<Value, Value>(RValueEqualityComparer.instance);
		}

		public bool ContainsKey(string identifier) {
			return map.ContainsKey(ValString.Temp(identifier));
		}

		public bool ContainsKey(Value key) {
			return map.ContainsKey(key);
		}

		public int Count {
			get { return map.Count; }
		}

		public Dictionary<Value, Value>.KeyCollection Keys {
			get { return map.Keys; }
		}

		public Value this [string identifier] {
			get { return map[ValString.Temp(identifier)]; }
			set { map[new ValString(identifier)] = value; }
		}

		public override Value FullEval(TAC.Context context) {
			// Evaluate each of our elements, and if any of those is
			// a variable or temp, then resolve those now.
			foreach (Value k in map.Keys.ToArray()) {	// TODO: something more efficient here.
				Value key = k;		// stupid C#!
				Value value = map[key];
				if (key is ValTemp || key is ValVar) {
					map.Remove(key);
					key = key.Val(context);
					map[key] = value;
				}
				if (value is ValTemp || value is ValVar) {
					map[key] = value.Val(context);
				}
			}
			return this;
		}

		public ValMap EvalCopy(TAC.Context context) {
			// Create a copy of this map, evaluating its members as we go.
			// This is used when a map literal appears in the source, to
			// ensure that each time that code executes, we get a new, distinct
			// mutable object, rather than the same object multiple times.
			ValMap result = new ValMap();
			foreach (Value k in map.Keys) {
				Value key = k;		// stupid C#!
				Value value = map[key];
				if (key is ValTemp || key is ValVar) key = key.Val(context);
				if (value is ValTemp || value is ValVar) value = value.Val(context);
				result.map[key] = value;
			}
			return result;
		}

		public override string CodeForm(int recursionLimit=-1) {
			if (recursionLimit == 0) return "{...}";
			string[] strs = new string[map.Count];
			int i = 0;
			foreach (KeyValuePair<Value, Value> kv in map) {
				strs[i++] = string.Format("{0}: {1}", kv.Key.CodeForm(recursionLimit-1), 
					kv.Value == null ? "null" : kv.Value.CodeForm(recursionLimit-1));
			}
			return "{" + String.Join(", ", strs) + "}";
		}

		public override string ToString() {
			return CodeForm(3);
		}

		public override int Hash() {
			return map.GetHashCode();
//			int result = map.Count.GetHashCode();
//			foreach (KeyValuePair<Value, Value> kv in map) {
//				result ^= kv.Key.Hash();
//				result ^= kv.Value.Hash();
//			}
//			return result;
		}

		public override bool Equals(Value rhs) {
			if (!(rhs is ValMap)) return false;
			Dictionary<Value, Value> rhm = ((ValMap)rhs).map;
			int count = map.Count;
			if (count != rhm.Count) return false;
			foreach (KeyValuePair<Value, Value> kv in map) {
				if (!rhm.ContainsKey(kv.Key)) return false;
				if (!kv.Value.Equals(rhm[kv.Key])) return false;
			}
			return true;
		}

		public override bool CanSetElem() { return true; }

		public override void SetElem(Value index, Value value) {
			map[index] = value;
		}

		/// <summary>
		/// Get the indicated key/value pair as another map containing "key" and "value".
		/// </summary>
		/// <param name="index">0-based index of key/value pair to get.</param>
		public ValMap GetKeyValuePair(int index) {
			Dictionary<Value, Value>.KeyCollection keys = map.Keys;
			if (index < 0 || index >= keys.Count) {
				throw new IndexException("index " + index.ToString() + " out of range for map");
			}
			Value key = keys.ElementAt<Value>(index);	// (TODO: consider more efficient methods here)
			ValMap result = new ValMap();
			result.map[keyStr] = key;
			result.map[valStr] = map[key];
			return result;
		}
		static ValString keyStr = new ValString("key");
		static ValString valStr = new ValString("value");

	}

	public class Function {
		public class Param {
			public string name;
			public Value defaultValue;

			public Param(string name, Value defaultValue) {
				this.name = name;
				this.defaultValue = defaultValue;
			}
		}

		public List<Param> parameters;
		public List<TAC.Line> code;

		public Function(List<TAC.Line> code) {
			this.code = code;
			parameters = new List<Param>();
		}

		public override string ToString() {
			return string.Format("FUNCTION({0})", string.Join(", ", 
				parameters.Select(p => p.name).ToArray()));
		}
	}

	public class ValFunction : Value {
		public Function function;

		public ValFunction(Function function) {
			this.function = function;
		}

		public override string ToString() {
			return function.ToString();
		}

		public override bool BoolValue() {
			return true;
		}

		public override int Hash() {
			return function.GetHashCode();
		}

		public override bool Equals(Value rhs) {
			if (!(rhs is ValFunction)) return false;
			ValFunction other = (ValFunction)rhs;
			return function == other.function;
		}

	}

	public class ValTemp : Value {
		public int tempNum;

		public ValTemp(int tempNum) {
			this.tempNum = tempNum;
		}

		public override Value Val(TAC.Context context) {
			return context.GetTemp(tempNum);
		}

		public override string ToString() {
			return "_" + tempNum;
		}

		public override int Hash() {
			return tempNum.GetHashCode();
		}

		public override bool Equals(Value rhs) {
			return rhs is ValTemp && ((ValTemp)rhs).tempNum == tempNum;
		}

	}

	public class ValVar : Value {
		public string identifier;

		public ValVar(string identifier) {
			this.identifier = identifier;
		}

		public override Value Val(TAC.Context context) {
			return context.GetVar(identifier);
		}

		public override string ToString() {
			return identifier;
		}

		public override int Hash() {
			return identifier.GetHashCode();
		}

		public override bool Equals(Value rhs) {
			return rhs is ValVar && ((ValVar)rhs).identifier == identifier;
		}

		// Special name for the implicit result variable we assign to on expression statements:
		public static ValVar implicitResult = new ValVar("_");
	}

	public class ValSeqElem : Value {
		public Value sequence;
		public Value index;

		public ValSeqElem(Value sequence, Value index) {
			this.sequence = sequence;
			this.index = index;
		}

		/// <summary>
		/// Look up the given identifier in the given sequence, walking the type chain
		/// until we either find it, or fail.
		/// </summary>
		/// <param name="sequence">Sequence (object) to look in.</param>
		/// <param name="identifier">Identifier to look for.</param>
		/// <param name="context">Context.</param>
		public static Value Resolve(Value sequence, string identifier, TAC.Context context) {
			bool includeMapType = true;
			while (sequence != null) {
				if (sequence is ValTemp || sequence is ValVar) sequence = sequence.Val(context);
				if (sequence is ValMap) {
					// If the map contains this identifier, return its value.
					Value result = null;
					if (((ValMap)sequence).map.TryGetValue(ValString.Temp(identifier), out result)) return result;
					// Otherwise, if we have an __isa, try that next
					if (!((ValMap)sequence).map.TryGetValue(ValString.magicIsA, out sequence)) {
						// ...and if we don't have an __isa, try the generic map type if allowed
						if (!includeMapType) throw new KeyException(identifier);
						sequence = Intrinsics.MapType();
						includeMapType = false;
					}
				} else if (sequence is ValList) {
					sequence = Intrinsics.ListType();
					includeMapType = false;
				} else if (sequence is ValString) {
					sequence = Intrinsics.StringType();
					includeMapType = false;
				} else {
					throw new TypeException("Type Error (while attempting to look up " + identifier + ")");
				}
			}
			return null;
		}

		public override Value Val(TAC.Context context) {
			// There is a TAC opcode for looking this up.  But, when chaining
			// lookups, it's darned convenient to just ask each step to get its value.
			// SO:
			Value idxVal = index == null ? null : index.Val(context);
			if (idxVal is ValString) return Resolve(sequence, ((ValString)idxVal).value, context);
			// Ok, we're searching for something that's not a string;
			// this can only be done in maps and lists (and lists, only with a numeric index).
			Value baseVal = sequence.Val(context);
			if (baseVal is ValMap) {
				Value result = null;
				// Keep walking the "__isa" chain until we find the value, or can go no further.
				while (baseVal is ValMap) {
					if (idxVal == null) throw new KeyException("null");
					if (((ValMap)baseVal).map.TryGetValue(idxVal, out result)) return result;
					if (!((ValMap)baseVal).map.TryGetValue(ValString.magicIsA, out baseVal)) {
						throw new KeyException(idxVal.ToString());
					}
					baseVal = baseVal.Val(context);
				}
			} else if (baseVal is ValList && idxVal is ValNumber) {
				return ((ValList)baseVal).GetElem(idxVal);
			} else if (baseVal is ValString && idxVal is ValNumber) {
				return ((ValString)baseVal).GetElem(idxVal);
			}
				
			throw new TypeException("Type Exception: can't index into this type");
		}

		public override string ToString() {
			return string.Format("{0}[{1}]", sequence, index);
		}

		public override int Hash() {
			return sequence.GetHashCode() ^ index.GetHashCode();
		}

		public override bool Equals(Value rhs) {
			return rhs is ValSeqElem && ((ValSeqElem)rhs).sequence == sequence
				&& ((ValSeqElem)rhs).index == index;
		}

	}

	public class RValueEqualityComparer : IEqualityComparer<Value> {
		public bool Equals(Value val1, Value val2) {
			return val1.Equals(val2);
		}

		public int GetHashCode(Value val) {
			return val.Hash();
		}

		static RValueEqualityComparer _instance = null;
		public static RValueEqualityComparer instance {
			get {
				if (_instance == null) _instance = new RValueEqualityComparer();
				return _instance;
			}
		}
	}
}

