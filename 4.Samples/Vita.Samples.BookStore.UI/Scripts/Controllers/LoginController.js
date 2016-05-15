var LoginController = function ($scope, $rootScope, $stateParams, $state, $window, $location, $cookies, LoginService, SessionService) {
  $scope.title = 'BookStore';
  $rootScope.LoginMessage = "";
  if ($cookies.BookStoreToken != undefined && $cookies.BookStoreToken != null && $cookies.BookStoreToken != 'null') {
    $rootScope.IsLoggedIn = true;
  }
  else {
    $rootScope.IsLoggedIn = false;
  }


  // form for login and registration
  $scope.itemForm = {
    userName: '',
    password: '',
    id: "00000000-0000-0000-0000-000000000000",
    redirectUrl: $stateParams.redirectUrl || "",
    displayName: '',
    isActive: false,
    logins: null,
    selectedLogin: null
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

  // login api
  $scope.login = function () {
    var result = LoginService.login($scope.itemForm.userName, $scope.itemForm.password);
    result.then(function (result) {
      if (result.isSuccess) {
        if (result.data.Status == 'Failed') {
          $scope.status.isSuccess = false;
          $scope.status.isError = true;
          $rootScope.IsLoggedIn = false;
          $scope.status.errorMessage = "Login failed, please correct your username or password."
        } else {
          $scope.status.isSuccess = true;
          SessionService.setToken(result.data.AuthenticationToken);
          $rootScope.IsLoggedIn = true;
          $rootScope.LoginMessage = "Welcome " + result.data.UserName + "!";
          $scope.status.isReadOnly = true;
          $scope.status.isError = false;
          $scope.status.successMessage = "Login successful, welcome back " + result.data.UserName + "!";
          $scope.redirect();
        }
      } else {
        $scope.status.isError = true;
        $scope.status.isSuccess = false;
        $rootScope.IsLoggedIn = false;
        $scope.status.errorMessage = result.message;
      }
    });
  }

  // test login api
  $scope.testLogin = function () {
    var login = JSON.parse($scope.itemForm.selectedLogin);
    var result = LoginService.login(login.UserName, login.Password);
    result.then(function (result) {
      if (result.isSuccess) {
        if (result.data.Status == 'Failed') {
          $scope.status.isSuccess = false;
          $scope.status.isError = true;
          $rootScope.IsLoggedIn = false;
          $scope.status.errorMessage = "Login failed, please choose another test login."
        } else {
          $scope.status.isSuccess = true;
          SessionService.setToken(result.data.AuthenticationToken);
          $rootScope.IsLoggedIn = true;
          $rootScope.LoginMessage = "Welcome " + result.data.UserName + "!";
          $scope.status.isReadOnly = true;
          $scope.status.isError = false;
          $scope.status.successMessage = "Login successful, welcome back " + result.data.UserName + "!";
          $scope.redirect();
        }
      } else {
        $scope.status.isError = true;
        $scope.status.isSuccess = false;
        $rootScope.IsLoggedIn = false;
        $scope.status.errorMessage = result.message;
      }
    });
  }
  // logout api
  $scope.logout = function () {
    var result = LoginService.logout();
    result.then(function (result) {
      $rootScope.IsLoggedIn = false;
      if (result.isSuccess) {
        $scope.status.isSuccess = true;
        SessionService.setToken(null);
        $scope.status.isReadOnly = true;
        $scope.status.isError = false;
        $scope.status.successMessage = "Logout successful, come back soon!"
      } else {
        $scope.status.isError = true;
        $scope.status.isSuccess = false;
        $scope.status.errorMessage = result.message;
      }
    });
  }

  // register  api
  $scope.register = function () {
    var result = LoginService.register($scope.itemForm.userName, $scope.itemForm.password, $scope.itemForm.id, $scope.itemForm.displayName, $scope.itemForm.isActive);
    result.then(function (result) {
      if (result.isSuccess) {
        var result2 = LoginService.login($scope.itemForm.userName, $scope.itemForm.password);
        result2.then(function (result2) {
          if (result2.isSuccess) {
            if (result2.data.Status == 'Failed') {
              $scope.status.isSuccess = false;
              $scope.status.isError = true;
              $rootScope.IsLoggedIn = false;
              $scope.status.errorMessage = "Login failed after registration, please try logging in again."
            } else {
              $scope.status.isSuccess = true;
              SessionService.setToken(result2.data.AuthenticationToken);
              $rootScope.IsLoggedIn = true;
              $rootScope.LoginMessage = "Welcome " + result2.data.UserName + "!";
              $scope.status.isReadOnly = true;
              $scope.status.isError = false;
              $scope.status.successMessage = "You have been registered, welcome aboard!"
            }
          } else {
            $scope.status.isError = true;
            $scope.status.isSuccess = false;
            $rootScope.IsLoggedIn = false;
            $scope.status.errorMessage = result.message;
          }
        });
      } else {
        $scope.status.isError = true;
        $scope.status.isSuccess = false;
        $rootScope.IsLoggedIn = false;
        $scope.status.errorMessage = result.message;
      }
    });
  }

  // navigation and other functions
  $scope.back = function () {
    $window.history.back();
  }

  // navigation and other functions
  $scope.redirect = function () {
    if ($scope.itemForm.redirectUrl != '') {
      $window.location.href = $scope.itemForm.redirectUrl;
    }
    else {
      $state.go('search');
    }
  }
}

LoginController.$inject = ['$scope', '$rootScope', '$stateParams', '$state', '$window', '$location', '$cookies', 'LoginService', 'SessionService'];