using System.Collections.Generic;
using System.Linq;

namespace ToolCheckCmt {
    public class TokenManager {
        private List<string> _listTokens = new List<string>();
        private int _currentTokenIndex = 0;
        private readonly object _tokenLock = new object();

        public void LoadTokens(IEnumerable<string> tokens) {
            lock (_tokenLock) {
                _listTokens = tokens.Where(x => x.Length > 10).Distinct().ToList();
                _currentTokenIndex = 0;
            }
        }

        public string GetNextToken() {
            lock (_tokenLock) {
                if (_listTokens.Count == 0) return "";
                string token = _listTokens[_currentTokenIndex];
                _currentTokenIndex++;
                if (_currentTokenIndex >= _listTokens.Count) _currentTokenIndex = 0;
                return token;
            }
        }

        public void RemoveDeadToken(string token) {
            lock (_tokenLock) {
                if (_listTokens.Contains(token)) {
                    _listTokens.Remove(token);
                    if (_listTokens.Count > 0 && _currentTokenIndex >= _listTokens.Count) {
                        _currentTokenIndex = 0;
                    }
                }
            }
        }

        public int GetAliveCount() {
            lock (_tokenLock) { return _listTokens.Count; }
        }

        public List<string> GetAllAliveTokens() {
            lock (_tokenLock) { return new List<string>(_listTokens); }
        }
    }
}