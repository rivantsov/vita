/*<copyright>
Should a copyright go here?
</copyright>*/

//------------------------------------------------------------------------------
/// This file manages controllers, states, and other details for the
/// primary angular module for this application.
//------------------------------------------------------------------------------

// define module with ui router
var DomainApp = angular.module('DomainApp', ['ui.router', 'ui.bootstrap', 'angularValidator', 'ngCookies']);

// add controllers, services, and factories
DomainApp.controller('UsersController', UsersController);
DomainApp.controller('KidBooksController', KidBooksController);
DomainApp.controller('FictionBooksController', FictionBooksController);
DomainApp.controller('ProgrammingBooksController', ProgrammingBooksController);
DomainApp.controller('BooksController', BooksController);
DomainApp.controller('CartController', CartController);
DomainApp.controller('LoginController', LoginController);

DomainApp.service('UsersService', UsersService);
DomainApp.service('BooksService', BooksService);
DomainApp.service('OrdersService', OrdersService);
DomainApp.service('LoginService', LoginService);
DomainApp.service('SessionService', SessionService);
DomainApp.factory('WebConfigFactory', WebConfigFactory);

var configFunction = function ($stateProvider, $httpProvider, $locationProvider) {
    
    $locationProvider.hashPrefix('!').html5Mode(true);
    
    $httpProvider.interceptors.push(function ($q, $cookies) {
        return {
            'request': function ($config) {
                if ($cookies.BookStoreToken != undefined) {
                    $config.headers['Authorization'] = $cookies.BookStoreToken;
                }
                return $config;
            }
        }
    });
    
    $stateProvider
        .state('loginUser', {
            url: '/login?redirectUrl',
            views: {
                "detailView": {
                    templateUrl: '/Templates/login/Login.html',
                    controller: LoginController
                }
            }
        })
        .state('registerUser', {
        	url: '/register?redirectUrl',
            views: {
                "detailView": {
                    templateUrl: '/Templates/login/Register.html',
                    controller: LoginController
                }
            }
        })
        .state('programming', {
            url: '/books/programming?orderBy&descending&page&pageSize',
            views: {
                "detailView": {
                    templateUrl: '/Templates/Books/Programming.html',
                    controller: ProgrammingBooksController
                }
            }
        })
        .state('fiction', {
          url: '/books/fiction?orderBy&descending&page&pageSize',
            views: {
                "detailView": {
                    templateUrl: '/Templates/Books/Fiction.html',
                    controller: FictionBooksController
                }
            }
        })
        .state('kids', {
            url: '/books/kids?orderBy&descending&page&pageSize',
            views: {
                "detailView": {
                    templateUrl: '/Templates/Books/Kids.html',
                    controller: KidBooksController
                }
            }
        })
        .state('search', {
          url: '/books?title&categories&maxPrice&publisher&publishedBefore&publishedAfter&authorLastName&orderBy&descending&page&pageSize',
          views: {
                "searchView": {
                    templateUrl: '/Templates/Books/Search.html',
                    controller: BooksController
                },
                "detailView": {
                    templateUrl: '/Templates/Books/Results.html',
                    controller: BooksController
                }
            }
        })
        .state('bookGet', {
            url: '/books/get?id',
            views: {
                "detailView": {
                    templateUrl: '/Templates/books/Get.html',
                    controller: BooksController
                }
            }
        })
        .state('cart', {
            url: '/cart?bookId',
            views: {
                "detailView": {
                    templateUrl: '/Templates/Cart/Home.html',
                    controller: CartController
                }
            }
        })
        .state('order', {
          url: '/order',
          views: {
            "detailView": {
              templateUrl: '/Templates/Order/Home.html',
              controller: CartController
            }
          }
        })
        .state('myorders', {
          url: '/myorders',
          views: {
            "detailView": {
              templateUrl: '/Templates/Order/Orders.html',
              controller: CartController
            }
          }
        })
        .state('myorder', {
          url: '/myorder?orderId',
          views: {
            "detailView": {
              templateUrl: '/Templates/Order/Order.html',
              controller: CartController
            }
          }
        })
        .state('home', {
            url: '/',
            views: {
              "detailView": {
                templateUrl: '/Templates/Home/Home.html',
              }
            }
        });
}
configFunction.$inject = ['$stateProvider', '$httpProvider', '$locationProvider'];

DomainApp.config(configFunction);