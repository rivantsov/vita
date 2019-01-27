using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Vita.Entities.Utilities {

  // Customized version of BitArray
  // https://referencesource.microsoft.com/#mscorlib/system/collections/bitarray.cs

  public sealed class BitMask {
    private const int BitsPerInt32 = 32;
    private const int BytesPerInt32 = 4;
    private const int BitsPerByte = 8;
    private const uint _allSet = 0xFFFFFFFF;

    private int[] _array;
    private int _length;

    public BitMask(int length, bool setAll = false) {
      _length = length;
      var numInts = ((length - 1) / BitsPerInt32) + 1;
      _array = new int[numInts];
      if(setAll)
        SetAll(true); 
    }

    private BitMask(int length, int[] array) {
      _length = length;
      _array = array; 
    }

    public bool this[int index] {
      get { return Get(index); }
      set { Set(index, value);  }
    }

    public bool Get(int index) {
      return (_array[index / 32] & (1 << (index % 32))) != 0;
    }

    public void Set(int index, bool value) {
      if(value) {
        _array[index / 32] |= (1 << (index % 32));
      } else {
        _array[index / 32] &= ~(1 << (index % 32));
      }
    }

    public void SetAll(bool value) {
      int fillValue = value ? unchecked(((int)0xffffffff)) : 0;
      for(int i = 0; i < _array.Length; i++) {
        _array[i] = fillValue;
      }
    }

    public BitMask And(BitMask value) {
      for(int i = 0; i < _array.Length; i++) {
        _array[i] &= value._array[i];
      }
      return this;
    }

    public BitMask Or(BitMask value) {
      for(int i = 0; i < _array.Length; i++) {
        _array[i] |= value._array[i];
      }
      return this;
    }

    public BitMask Xor(BitMask value) {
      for(int i = 0; i < _array.Length; i++) {
        _array[i] ^= value._array[i];
      }
      return this;
    }

    public BitMask Not() {
      for(int i = 0; i < _array.Length; i++) {
        _array[i] = ~_array[i];
      }
      return this;
    }

    public int Length {
      get { return _length; }
    }

    public int[] GetData() {
      return _array;
    }

    public string ToHex() {
      if(_array.Length == 1) //most common case
        return HexUtil.IntToHex(_array[0]);
      return string.Join(string.Empty, _array.Select(v => HexUtil.IntToHex(v)));
    }

    // ICollection implementation
    public void CopyTo(Array array) {
      Array.Copy(_array, array, _array.Length);
    }

    public int Count {
      get {
        return _length;
      }
    }

    public Object Clone() {
      BitMask bitArray = new BitMask(_length, _array);
      bitArray._length = _length;
      return bitArray;
    }

  }
}
