using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vita.Common {

  // derived from System.Data.Linq.Binary class - to avoid referencing System.Data.Linq assembly
  // Also provides an option in constructor to avoid copying the byte array
  [Serializable]
  public sealed class Binary : IEquatable<Binary> {
    byte[] _bytes;
    int? _hashCode;

    public Binary(byte[] value, bool makeCopy = true) {
      if (value == null) {
        this._bytes = new byte[0];
      } else {
        if (makeCopy) {
          this._bytes = new byte[value.Length];
          Array.Copy(value, this._bytes, value.Length);
        } else
          _bytes = value; 
      }
      this.ComputeHash();
    }

    public byte[] ToArray() {
      byte[] copy = new byte[this._bytes.Length];
      Array.Copy(this._bytes, copy, copy.Length);
      return copy;
    }

    public int Length {
      get { return this._bytes.Length; }
    }

    public static implicit operator Binary(byte[] value) {
      return new Binary(value);
    }

    public bool Equals(Binary other) {
      return this.EqualsTo(other);
    }

    public static bool operator ==(Binary binary1, Binary binary2) {
      if ((object)binary1 == (object)binary2)
        return true;
      if ((object)binary1 == null && (object)binary2 == null)
        return true;
      if ((object)binary1 == null || (object)binary2 == null)
        return false;
      return binary1.EqualsTo(binary2);
    }

    public static bool operator !=(Binary binary1, Binary binary2) {
      if ((object)binary1 == (object)binary2)
        return false;
      if ((object)binary1 == null && (object)binary2 == null)
        return false;
      if ((object)binary1 == null || (object)binary2 == null)
        return true;
      return !binary1.EqualsTo(binary2);
    }

    public override bool Equals(object obj) {
      return this.EqualsTo(obj as Binary);
    }

    public override int GetHashCode() {
      if (!_hashCode.HasValue) {
        // hash code is not marked [DataMember], so when 
        // using the DataContractSerializer, we'll need
        // to recompute the hash after deserialization. 
        ComputeHash();
      }
      return this._hashCode.Value;
    }

    public override string ToString() {
      StringBuilder sb = new StringBuilder();
      sb.Append("\"");
      sb.Append(System.Convert.ToBase64String(this._bytes, 0, this._bytes.Length));
      sb.Append("\"");
      return sb.ToString();
    }

    private bool EqualsTo(Binary binary) {
      if ((object)this == (object)binary)
        return true;
      if ((object)binary == null)
        return false;
      if (this._bytes.Length != binary._bytes.Length)
        return false;
      if (this.GetHashCode() != binary.GetHashCode())
        return false;
      for (int i = 0, n = this._bytes.Length; i < n; i++) {
        if (this._bytes[i] != binary._bytes[i])
          return false;
      }
      return true;
    }

    /// <summary> 
    /// Simple hash using pseudo-random coefficients for each byte in
    /// the array to achieve order dependency.
    /// </summary>
    private void ComputeHash() {
      int s = 314, t = 159;
      _hashCode = 0;
      for (int i = 0; i < _bytes.Length; i++) {
        _hashCode = _hashCode * s + _bytes[i];
        s = s * t;
      }
    }

    //extra method for efficiency, to avoid duplicating array
    public byte[] GetBytes() {
      return _bytes; 
    }
  }


}
