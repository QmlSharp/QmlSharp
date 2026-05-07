cmake_minimum_required(VERSION 3.26 FATAL_ERROR)

project(qmlsharp_native LANGUAGES CXX)

set(CMAKE_AUTOMOC ON)
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

find_package(Qt6 REQUIRED COMPONENTS Quick Qml)

set(QMLSHARP_NATIVE_SOURCE_DIR "${CMAKE_CURRENT_LIST_DIR}/../../../native")

file(GLOB_RECURSE QMLSHARP_NATIVE_CORE_SOURCES CONFIGURE_DEPENDS
  "${QMLSHARP_NATIVE_SOURCE_DIR}/src/*.cpp"
  "${QMLSHARP_NATIVE_SOURCE_DIR}/src/*.h"
  "${QMLSHARP_NATIVE_SOURCE_DIR}/include/qmlsharp/*.h"
)

add_library(qmlsharp_native SHARED
  ${QMLSHARP_NATIVE_CORE_SOURCES}
  CounterViewModel.cpp
  CounterViewModel.h
  type_registration.cpp
)

target_compile_features(qmlsharp_native PRIVATE cxx_std_20)
target_compile_definitions(qmlsharp_native PRIVATE QMLSHARP_NATIVE_BUILD)
target_include_directories(qmlsharp_native
  PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}
    ${QMLSHARP_NATIVE_SOURCE_DIR}/include
    ${QMLSHARP_NATIVE_SOURCE_DIR}/src
)
target_link_libraries(qmlsharp_native
  PRIVATE
    Qt6::Quick
    Qt6::Qml
)
