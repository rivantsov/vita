/*<copyright>
Should a copyright go here?
</copyright>*/

//------------------------------------------------------------------------------
/// <summary>This controller provides access to CRUD operations on
/// IUser items.</summary>
///
/// This file is code generated and should not be modified by hand.
/// If you need to customize outside of protected areas, add those changes
/// in another partial class file.  As a last resort (if generated code needs
/// to be different), change the Status value below to something other than
/// Generated to prevent changes from being overwritten.
///
/// <CreatedByUserName>INCODE-1\Dave</CreatedByUserName>
/// <CreatedDate>2/26/2015</CreatedDate>
/// <Status>Generated</Status>
//------------------------------------------------------------------------------
var UsersController = function($scope, $stateParams, $state, $window, $location, UsersService, OrdersService) {
    // data for search operations
    $scope.searchQuery = {
        orderBy: $stateParams.orderBy || '',
        descending: $stateParams.descending || 'false',
        page: $stateParams.page || 1,
        pageSize: $stateParams.pageSize || 10,
        totalPages: 0,
        filter: 'none'
    };
    
    // data to get search results
    $scope.searchResults = {
        items: null,
        totalPages: 0,
        totalItems: 0,
        hasResults: false,
        canCreate: true
    };
    
    // data to get an item
    $scope.itemQuery = {
        id: $stateParams.id || "00000000-0000-0000-0000-000000000000",
        itemFound: false
    };
    
    // data for create, update, and delete operations
    $scope.itemForm = {
        id: "00000000-0000-0000-0000-000000000000",
        userName: '',
        userNameHash: 0,
        displayName: '',
        isActive: false,
        userBookReviews: null,
        userBookOrders: null,
        userAuthors: null,
        canEdit: false,
        canDelete: false
    };
    
    // status on any operation
    $scope.status = {
        isReadOnly: false,
        isError: false,
        errorMessage: '',
        isSuccess: false,
        successMessage: ''
    };
    
    $scope.navbarProperties = {
        isCollapsed: true
    };
    
    // search api
    $scope.search = function () {
        $scope.searchQuery.filter = '';
        if ($scope.searchQuery.filter == '') {
            $scope.searchQuery.filter = 'none';
        }
        var result = UsersService.searchUsers($scope.searchQuery.orderBy, $scope.searchQuery.descending, $scope.searchQuery.page, $scope.searchQuery.pageSize);
        result.then(function(result) {
            if (result.isSuccess) {
                $scope.searchResults.items = result.items;
                $scope.searchResults.totalPages = Math.ceil(1.0 * result.totalItems / $scope.searchQuery.pageSize);
                $scope.searchResults.totalItems = result.totalItems;
                $scope.searchResults.hasResults = true;
                $scope.searchResults.canCreate = result.canCreate;
            } else {
                $scope.status.isError = true;
                $scope.status.isSuccess = false;
                $scope.status.errorMessage = result.message;
            }
        });
    }
    
    $scope.refreshSearch = function () {
        $state.go('userResults', {
            'orderBy': $scope.searchQuery.orderBy,
            'descending': $scope.searchQuery.descending,
            'page': $scope.searchQuery.page,
            'pageSize': $scope.searchQuery.pageSize
        });
    }
    
    // get api
    $scope.get = function (isEdit) {
        var result = UsersService.getUser($scope.itemQuery.id);
        result.then(function(result) {
            if (result.isSuccess) {
                $scope.status.isSuccess = true;
                $scope.itemForm.id = result.data.Id;
                $scope.itemForm.userName = result.data.UserName;
                $scope.itemForm.userNameHash = result.data.UserNameHash;
                $scope.itemForm.displayName = result.data.DisplayName;
                $scope.itemForm.isActive = result.data.IsActive;
                $scope.itemForm.canEdit = result.data.CanEdit;
                $scope.itemForm.canDelete = result.data.CanDelete;
                if (isEdit == true && $scope.itemForm.canEdit == false) {
                    $scope.status.isReadOnly = true;
                }
                $scope.init();
            } else {
                $scope.status.isError = true;
                $scope.status.isSuccess = false;
                $scope.status.errorMessage = result.message;
            }
        });
    }
    
    // get UserBookOrders api
    $scope.getUserBookOrders = function () {
        var editing = $stateParams.editing;
        if (editing != 'true' && editing != true) {
            $scope.status.isReadOnly = true;
        }
        var result = UsersService.getUser($scope.itemQuery.id);
        result.then(function(result) {
            if (result.isSuccess) {
                $scope.status.isSuccess = true;
                $scope.itemForm.id = result.data.Id;
                $scope.itemForm.userName = result.data.UserName;
                $scope.itemForm.userNameHash = result.data.UserNameHash;
                $scope.itemForm.displayName = result.data.DisplayName;
                $scope.itemForm.isActive = result.data.IsActive;
                $scope.init();
                var searchResult = OrdersService.searchBookOrders($scope.itemQuery.id, $scope.searchQuery.orderBy, $scope.searchQuery.descending, $scope.searchQuery.page, $scope.searchQuery.pageSize);
                searchResult.then(function(result) {
                    if (result.isSuccess) {
                        $scope.itemForm.userBookOrders = result.items;
                    } else {
                        $scope.status.isError = true;
                        $scope.status.isSuccess = false;
                        $scope.status.errorMessage = result.message;
                    }
                });
            } else {
                $scope.status.isError = true;
                $scope.status.isSuccess = false;
                $scope.status.errorMessage = result.message;
            }
        });
    }
    
    // create api
    $scope.create = function () {
        var result = UsersService.createUser($scope.itemForm.userName, $scope.itemForm.userNameHash, $scope.itemForm.displayName, $scope.itemForm.isActive);
        result.then(function(result) {
            if (result.isSuccess) {
                $scope.status.isSuccess = true;
                $scope.status.isReadOnly = true;
                $scope.status.isError = false;
                $scope.status.successMessage = "User item successfully created."
            } else {
                $scope.status.isError = true;
                $scope.status.isSuccess = false;
                $scope.status.errorMessage = result.message;
            }
        });
    }
    
    // update api
    $scope.update = function () {
        var result = UsersService.updateUser($scope.itemForm.id, $scope.itemForm.userName, $scope.itemForm.userNameHash, $scope.itemForm.displayName, $scope.itemForm.isActive);
        result.then(function(result) {
            if (result.isSuccess) {
                $scope.status.isSuccess = true;
                $scope.status.isReadOnly = true;
                $scope.status.isError = false;
                $scope.status.successMessage = "User item successfully updated."
            } else {
                $scope.status.isError = true;
                $scope.status.isSuccess = false;
                $scope.status.errorMessage = result.message;
            }
        });
    }
    
    // delete api
    $scope.delete = function () {
        var result = UsersService.deleteUser($scope.itemQuery.id);
        result.then(function(result) {
            if (result.isSuccess) {
                $scope.status.isSuccess = true;
                $scope.status.isReadOnly = true;
                $scope.status.isError = false;
                $scope.status.successMessage = "User item successfully deleted."
            } else {
                $scope.status.isError = true;
                $scope.status.isSuccess = false;
                $scope.status.errorMessage = result.message;
            }
        });
    }
    
    // navigation and other functions
    $scope.back = function() {
        $window.history.back();
    }
    
    $scope.first = function () {
        $scope.searchQuery.page = 1;
        $scope.refreshSearch();
    }
    
    $scope.next = function () {
        $scope.searchQuery.page = parseInt($scope.searchQuery.page) + 1;
        $scope.refreshSearch();
    }
    
    $scope.previous = function () {
        $scope.searchQuery.page = parseInt($scope.searchQuery.page) - 1;
        $scope.refreshSearch();
    }
    
    $scope.last = function () {
        $scope.searchQuery.page = $scope.searchResults.totalPages;
        $scope.refreshSearch();
    }
    
    // init api
    $scope.init = function () {
    }
}

UsersController.$inject = ['$scope', '$stateParams', '$state', '$window', '$location', 'UsersService', 'OrdersService'];