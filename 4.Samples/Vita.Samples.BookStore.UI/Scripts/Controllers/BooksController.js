/*<copyright>
Should a copyright go here?
</copyright>*/

//------------------------------------------------------------------------------
/// <summary>This controller provides access to CRUD operations on
/// IBook items.</summary>
//------------------------------------------------------------------------------
var BooksController = function($scope, $stateParams, $state, $window, $location, BooksService) {
    // data for search operations
    $scope.searchQuery = {
        title: $stateParams.title || "",
        categories: $stateParams.categories || "",
        maxPrice: $stateParams.maxPrice || "",
        publisher: $stateParams.publisher || "",
        publishedAfter: $stateParams.publishedAfter || "",
        publishedBefore: $stateParams.publishedBefore || "",
        authorLastName: $stateParams.authorLastName || "",
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
        hasResults: false
    };
    
    // data to get an item
    $scope.itemQuery = {
        id: $stateParams.id || "00000000-0000-0000-0000-000000000000",
        itemFound: false
    };
    
    // data for create, update, and delete operations
    $scope.itemForm = {
        id: "00000000-0000-0000-0000-000000000000",
        title: '',
        description: '',
        publishedOn: null,
        abstract: '',
        category: 0,
        editions: 0,
        price: 0.0,
        formattedPrice: "$0",
        publisherId: $stateParams.publisherId || "00000000-0000-0000-0000-000000000000",
        publishers: null,
        bookReviews: null,
        bookOrderLines: null,
        coverImage: null,
        authors: null,
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
        var result = BooksService.searchBooks($scope.searchQuery.title, $scope.searchQuery.categories, $scope.searchQuery.maxPrice, $scope.searchQuery.publisher, $scope.searchQuery.publishedAfter, $scope.searchQuery.publishedBefore, $scope.searchQuery.authorLastName, $scope.searchQuery.orderBy, $scope.searchQuery.descending, $scope.searchQuery.page, $scope.searchQuery.pageSize);
        result.then(function(result) {
            if (result.isSuccess) {
                $scope.searchResults.items = result.items;
                $scope.searchResults.totalPages = Math.ceil(1.0 * result.totalItems / $scope.searchQuery.pageSize);
                $scope.searchResults.totalItems = result.totalItems;
                $scope.searchResults.hasResults = true;
            } else {
                $scope.status.isError = true;
                $scope.status.isSuccess = false;
                $scope.status.errorMessage = result.message;
            }
        });
    }
    
    $scope.refreshSearch = function () {
      $state.go('search', {
            'title': $scope.searchQuery.title,
            'categories': $scope.searchQuery.categories,
            'maxPrice': $scope.searchQuery.maxPrice,
            'publisher': $scope.searchQuery.publisher,
            'publishedBefore': $scope.searchQuery.publishedBefore,
            'publishedAfter': $scope.searchQuery.publishedAfter,
            'authorLastName': $scope.searchQuery.authorLastName,
            'orderBy': $scope.searchQuery.orderBy,
            'descending': $scope.searchQuery.descending,
            'page': $scope.searchQuery.page,
            'pageSize': $scope.searchQuery.pageSize
        });
    }
    
    // get api
    $scope.get = function (isEdit) {
        var result = BooksService.getBook($scope.itemQuery.id);
        result.then(function(result) {
            if (result.isSuccess) {
                $scope.status.isSuccess = true;
                $scope.itemForm.id = result.data.Id;
                $scope.itemForm.title = result.data.Title;
                $scope.itemForm.description = result.data.Description;
                $scope.itemForm.publishedOn = result.data.PublishedOn;
                $scope.itemForm.abstract = result.data.Abstract;
                $scope.itemForm.category = result.data.Category;
                $scope.itemForm.editions = result.data.Editions;
                $scope.itemForm.price = result.data.Price;
                $scope.itemForm.publisher = result.data.Publisher;
                $scope.itemForm.publisherId = result.data.PublisherId;
                $scope.itemForm.authors = result.data.Authors;
                $scope.itemForm.canEdit = result.data.CanEdit;
                $scope.itemForm.canDelete = result.data.CanDelete;
                if (isEdit == true && $scope.itemForm.canEdit == false) {
                    $scope.status.isReadOnly = true;
                }
                var imageResult = BooksService.getImage(result.data.CoverImageId);
                imageResult.then(function (imageResult) {
                  if (imageResult.isSuccess) {
                    $scope.itemForm.coverImage = imageResult.data._buffer;
                  }
                });
                $scope.init();
            } else {
                $scope.status.isError = true;
                $scope.status.isSuccess = false;
                $scope.status.errorMessage = result.message;
            }
        });
    }
    
    // create api
    $scope.create = function () {
        var result = BooksService.createBook($scope.itemForm.title, $scope.itemForm.description, $scope.itemForm.publishedOn, $scope.itemForm.abstract, $scope.itemForm.category, $scope.itemForm.editions, $scope.itemForm.price, $scope.itemForm.publisherId);
        result.then(function(result) {
            if (result.isSuccess) {
                $scope.status.isSuccess = true;
                $scope.status.isReadOnly = true;
                $scope.status.isError = false;
                $scope.status.successMessage = "Book item successfully created."
            } else {
                $scope.status.isError = true;
                $scope.status.isSuccess = false;
                $scope.status.errorMessage = result.message;
            }
        });
    }
    
    // update api
    $scope.update = function () {
        var result = BooksService.updateBook($scope.itemForm.id, $scope.itemForm.title, $scope.itemForm.description, $scope.itemForm.publishedOn, $scope.itemForm.abstract, $scope.itemForm.category, $scope.itemForm.editions, $scope.itemForm.price, $scope.itemForm.publisherId);
        result.then(function(result) {
            if (result.isSuccess) {
                $scope.status.isSuccess = true;
                $scope.status.isReadOnly = true;
                $scope.status.isError = false;
                $scope.status.successMessage = "Book item successfully updated."
            } else {
                $scope.status.isError = true;
                $scope.status.isSuccess = false;
                $scope.status.errorMessage = result.message;
            }
        });
    }
    
    // delete api
    $scope.delete = function () {
        var result = BooksService.deleteBook($scope.itemQuery.id);
        result.then(function(result) {
            if (result.isSuccess) {
                $scope.status.isSuccess = true;
                $scope.status.isReadOnly = true;
                $scope.status.isError = false;
                $scope.status.successMessage = "Book item successfully deleted."
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
        var publisherResult = PublishersService.listPublisher($scope.itemForm.publisherId);
        publisherResult.then(function(result) {
            if (result.isSuccess) {
                $scope.itemForm.publishers = result.data.Items;
            } else {
                $scope.status.isError = true;
                $scope.status.isSuccess = false;
                $scope.status.errorMessage = result.message;
            }
        });
    }
}

BooksController.$inject = ['$scope', '$stateParams', '$state', '$window', '$location', 'BooksService'];