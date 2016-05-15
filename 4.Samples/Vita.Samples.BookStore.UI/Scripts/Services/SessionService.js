//------------------------------------------------------------------------------
/// <summary>This service provides access to session operations.</summary>
///
/// <CreatedByUserName>INCODE-1\Dave</CreatedByUserName>
/// <CreatedDate>2/26/2015</CreatedDate>
/// <Status>Generated</Status>
//------------------------------------------------------------------------------
var SessionService = function ($cookies) {
    this.token = null;
    this.getToken = function () {
        if(!$cookies.BookStoreToken) {
            if(!this.token) {
                return null;
            }
            this.setToken(this.token);
        }
        return $cookies.BookStoreToken;
    };
    
    this.setToken = function (token) {
        this.token = token;
        $cookies.BookStoreToken = token;
    };
}

SessionService.$inject = ['$cookies'];