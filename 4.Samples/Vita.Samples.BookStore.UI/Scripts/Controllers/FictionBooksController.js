/*<copyright>
Should a copyright go here?
</copyright>*/

//------------------------------------------------------------------------------
/// <summary>This controller provides Fiction books.</summary>
//------------------------------------------------------------------------------
var FictionBooksController = function ($scope, $stateParams, $state, $window, $location, BooksService) {
    // data for search operations
    $scope.searchQuery = {
        orderBy: $stateParams.orderBy || '',
        descending: $stateParams.descending === 'true' || false,
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
    };
    
    // status on any operation
    $scope.status = {
        isError: false,
        errorMessage: '',
        successMessage: ''
    };
    
    $scope.navbarProperties = {
        isCollapsed: true
    };
    
    // search api
    $scope.search = function () {
      var result = BooksService.searchBooks(null, "Fiction", null, null, null, null, null, $scope.searchQuery.orderBy, $scope.searchQuery.descending, $scope.searchQuery.page, $scope.searchQuery.pageSize);
        result.then(function(result) {
            if (result.isSuccess) {
                $scope.searchResults.items = result.items;
                $scope.searchResults.totalPages = Math.ceil(1.0 * result.totalItems / $scope.searchQuery.pageSize);
                $scope.searchResults.totalItems = result.totalItems;
                $scope.searchResults.hasResults = true;
            } else {
                $scope.status.isError = true;
                $scope.status.errorMessage = result.message;
            }
        });
    }

    $scope.refreshSearch = function () {
      $state.go('fiction', {
        'orderBy': $scope.searchQuery.orderBy,
        'descending': $scope.searchQuery.descending,
        'page': $scope.searchQuery.page,
        'pageSize': $scope.searchQuery.pageSize
      });
    }

    // navigation and other functions
    $scope.back = function () {
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

FictionBooksController.$inject = ['$scope', '$stateParams', '$state', '$window', '$location', 'BooksService'];