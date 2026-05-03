qmlsharp_find_qt()

set(CMAKE_AUTOMOC ON)

add_library(qmlsharp_native SHARED)

target_sources(qmlsharp_native
  PRIVATE
    src/qmlsharp_abi.cpp
    src/qmlsharp_engine.cpp
    src/qmlsharp_engine.h
    src/qmlsharp_errors.cpp
    src/qmlsharp_errors.h
    src/qmlsharp_instances.cpp
    src/qmlsharp_instances.h
    src/qmlsharp_type_registry.cpp
    src/qmlsharp_type_registry.h
  PUBLIC
    FILE_SET qmlsharp_native_headers
    TYPE HEADERS
    BASE_DIRS include
    FILES
      include/qmlsharp/qmlsharp_abi.h
      include/qmlsharp/qmlsharp_export.h
)

target_compile_features(qmlsharp_native PUBLIC cxx_std_20)
target_compile_definitions(qmlsharp_native PRIVATE QMLSHARP_NATIVE_BUILD)
target_include_directories(qmlsharp_native
  PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/include
  PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/src
)
target_link_libraries(qmlsharp_native
  PRIVATE
    Qt6::Core
    Qt6::Gui
    Qt6::Qml
    Qt6::Quick
)

set_target_properties(qmlsharp_native
  PROPERTIES
    OUTPUT_NAME qmlsharp_native
    RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin
    LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin
    ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib
)

qmlsharp_apply_warning_settings(qmlsharp_native)
qmlsharp_enable_sanitizers(qmlsharp_native)
qmlsharp_configure_static_analysis(qmlsharp_native)

if(QMLSHARP_BUILD_NATIVE_TESTS)
  add_subdirectory(tests)
endif()
