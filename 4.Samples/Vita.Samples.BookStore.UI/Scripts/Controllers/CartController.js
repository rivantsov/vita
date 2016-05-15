/*<copyright>
Should a copyright go here?
</copyright>*/

//------------------------------------------------------------------------------
/// <summary>This controller provides access to operations relating to
/// the Cart.</summary>
//------------------------------------------------------------------------------
var CartController = function ($scope, $rootScope, $stateParams, $state, $window, $location, UsersService, BooksService, OrdersService) {

  if (!$rootScope.IsLoggedIn) {
    if ($stateParams.bookId) {
      $state.go("loginUser", { redirectUrl: '/cart?bookId=' + $stateParams.bookId });
    }
    else {
      $state.go("loginUser", { redirectUrl: '/cart' });
    }
  }

  // data to get search results
  $scope.searchResults = {
    items: null,
    hasResults: false
  };

  // data for create, update, and delete operations
  $scope.itemForm = {
    bookId: $stateParams.bookId || null,
    orderId: $stateParams.orderId || "00000000-0000-0000-0000-000000000000",
    hasCartItems: false,
    coupon: $stateParams.coupon || null,
    Total: 0,
    Status: 0,
    items: null
  };

  // status on any operation
  $scope.status = {
    isError: false,
    errorMessage: '',
    isSuccess: false,
    successMessage: ''
  };

  $scope.navbarProperties = {
    isCollapsed: true
  };

  // init api
  $scope.init = function () {
    if ($scope.itemForm.bookId != null) {
      var result = OrdersService.addToCart($scope.itemForm.bookId);
      result.then(function (result) {
        if (result.isSuccess) {
          var result2 = OrdersService.getCart();
          result2.then(function (result2) {
            if (result2.isSuccess) {
              $scope.itemForm.Total = result2.data.Total;
              $scope.itemForm.items = result2.data.Items;
              if (result2.data.Items.length > 0) { $scope.itemForm.hasCartItems = true; } else { $scope.itemForm.hasCartItems = false; }
            } else {
              $scope.status.isSuccess = false;
              $scope.status.isError = true;
              $scope.status.errorMessage = "Could not get cart, please try again.";
            }
          });
        } else {
          $scope.status.isSuccess = false;
          $scope.status.isError = true;
          $scope.status.errorMessage = "Could not add item to cart, please try again.";
        }
      });
    }
    else {
      var result = OrdersService.getCart();
      result.then(function (result) {
        if (result.isSuccess) {
          $scope.itemForm.Total = result.data.Total;
          $scope.itemForm.items = result.data.Items;
          if (result.data.Items.length > 0) { $scope.itemForm.hasCartItems = true; } else { $scope.itemForm.hasCartItems = false; }
        } else {
          $scope.status.isSuccess = false;
          $scope.status.isError = true;
          $scope.status.errorMessage = "Could not get cart, please try again.";
        }
      });
    }
  }

  // save api
  $scope.save = function () {
    var result = OrdersService.saveCart($scope.itemForm.items);
    result.then(function (result) {
      if (result.isSuccess) {
        $scope.status.isSuccess = true;
        $scope.status.isError = false;
        $scope.status.successMessage = "Cart saved.";
        $scope.inputForm.$setPristine();
        $scope.itemForm.Total = result.data.Total;
        $scope.itemForm.items = result.data.Items;
        if (result.data.Items.length > 0) { $scope.itemForm.hasCartItems = true; } else { $scope.itemForm.hasCartItems = false; }
      } else {
        $scope.status.isSuccess = false;
        $scope.status.isError = true;
        $scope.status.errorMessage = result.message;
      }
    });
  }

  // order api
  $scope.order = function () {
    var result = OrdersService.placeOrder($scope.itemForm.coupon);
    result.then(function (result) {
      if (result.isSuccess) {
        $scope.status.isSuccess = true;
        $scope.status.isError = false;
        $scope.status.successMessage = "Your order has been placed!";
        $scope.itemForm.Total = result.data.Total;
        $scope.itemForm.items = result.data.Items;
        if (result.data.Items.length > 0) { $scope.itemForm.hasCartItems = true; } else { $scope.itemForm.hasCartItems = false; }
      } else {
        $scope.status.isSuccess = false;
        $scope.status.isError = true;
        $scope.status.errorMessage = result.message;
      }
    });
  }

  // get orders api
  $scope.orders = function () {
    var result = OrdersService.getOrders();
    result.then(function (result) {
      if (result.isSuccess) {
        $scope.status.isSuccess = true;
        $scope.status.isError = false;
        $scope.itemForm.items = result.items.Results;
      } else {
        $scope.status.isSuccess = false;
        $scope.status.isError = true;
        $scope.status.errorMessage = "Could not get orders, please try again.";
      }
    });
  }

  // get order api
  $scope.getOrder = function () {
    var result = OrdersService.getOrder($scope.itemForm.orderId);
    result.then(function (result) {
      if (result.isSuccess) {
        $scope.itemForm.Total = result.data.Total;
        $scope.itemForm.items = result.data.Items;
      } else {
        $scope.status.isSuccess = false;
        $scope.status.isError = true;
        $scope.status.errorMessage = "Could not get order, please try again.";
      }
    });
  }
}
CartController.$inject = ['$scope', '$rootScope', '$stateParams', '$state', '$window', '$location', 'UsersService', 'BooksService', 'OrdersService'];