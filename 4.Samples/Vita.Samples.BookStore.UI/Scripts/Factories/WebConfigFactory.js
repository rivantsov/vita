/*<copyright>
Should a copyright go here?
</copyright>*/

//------------------------------------------------------------------------------
/// This factory provides web configuration settings.
///
/// This file is code generated and should not be modified by hand.
/// If you need to customize outside of protected areas, change the Status
/// value below to something other than Generated to prevent changes from
/// being overwritten.
///
/// <CreatedByUserName>INCODE-1\Dave</CreatedByUserName>
/// <CreatedDate>2/26/2015</CreatedDate>
/// <Status>Generated</Status>
//------------------------------------------------------------------------------
var WebConfigFactory = function ($http, $q) {
    return {
        getSetting: function (setting) {
            
            var deferredObject = $q.defer();
            
            $http.get('/home/config?setting=' + setting).
                success(function (data) {
                    deferredObject.resolve(data);
                }).
                error(function () {
                    deferredObject.resolve('');
                });
            
            return deferredObject.promise;
        }
    }
}

WebConfigFactory.$inject = ['$http', '$q'];