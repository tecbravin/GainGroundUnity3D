mergeInto(LibraryManager.library, {
  GainGroundIsMobile: function () {
    return /Android|iPhone|iPad|iPod|Mobile/i.test(navigator.userAgent) ||
      (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1) ? 1 : 0;
  },
  GainGroundStorageGet: function (key) {
    try {
      var value = window.localStorage.getItem(UTF8ToString(key));
      if (value === null) return 0;
      var size = lengthBytesUTF8(value) + 1;
      var buffer = _malloc(size);
      stringToUTF8(value, buffer, size);
      return buffer;
    } catch (error) {
      console.warn('Gain Ground: browser storage unavailable', error);
      return 0;
    }
  },
  GainGroundStorageSet: function (key, value) {
    try {
      var name = UTF8ToString(key);
      var content = UTF8ToString(value);
      window.localStorage.setItem(name, content);
      return window.localStorage.getItem(name) === content ? 1 : 0;
    } catch (error) {
      console.warn('Gain Ground: could not save to browser storage', error);
      return 0;
    }
  },
  GainGroundStorageDelete: function (key) {
    try {
      window.localStorage.removeItem(UTF8ToString(key));
      return 1;
    } catch (error) {
      console.warn('Gain Ground: could not remove browser save', error);
      return 0;
    }
  }
});
