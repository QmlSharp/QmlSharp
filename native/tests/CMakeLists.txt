set(QMLSHARP_NATIVE_TEST_CORE_SOURCES
  ${QMLSHARP_NATIVE_SOURCES}
  ${QMLSHARP_NATIVE_PUBLIC_HEADERS}
)

file(GLOB_RECURSE QMLSHARP_NATIVE_TEST_FIXTURE_SOURCES CONFIGURE_DEPENDS
  ${CMAKE_CURRENT_SOURCE_DIR}/fixtures/*.cpp
  ${CMAKE_CURRENT_SOURCE_DIR}/fixtures/*.h
)
list(FILTER QMLSHARP_NATIVE_TEST_FIXTURE_SOURCES EXCLUDE REGEX ".*/qmlsharp_integration_fixture_exports\\.cpp$")

set(QMLSHARP_NATIVE_TEST_INTEGRATION_EXPORT_SOURCE
  ${CMAKE_CURRENT_SOURCE_DIR}/fixtures/qmlsharp_integration_fixture_exports.cpp
)

source_group(TREE ${CMAKE_CURRENT_SOURCE_DIR} FILES
  ${QMLSHARP_NATIVE_TEST_FIXTURE_SOURCES}
  ${QMLSHARP_NATIVE_TEST_INTEGRATION_EXPORT_SOURCE}
)

function(qmlsharp_configure_native_test_target target_name)
  target_compile_features(${target_name} PRIVATE cxx_std_20)
  target_compile_definitions(${target_name} PRIVATE QMLSHARP_NATIVE_BUILD)
  target_include_directories(${target_name}
    PRIVATE
      fixtures
      ${CMAKE_CURRENT_SOURCE_DIR}/../include
      ${CMAKE_CURRENT_SOURCE_DIR}/../src
  )
  target_link_libraries(${target_name}
    PRIVATE
      Qt6::Core
      Qt6::Gui
      Qt6::Qml
      Qt6::Quick
  )

  qmlsharp_apply_warning_settings(${target_name})
  qmlsharp_enable_sanitizers(${target_name})
  qmlsharp_configure_static_analysis(${target_name})
endfunction()

add_executable(qmlsharp_native_tests
  native_host_smoke_tests.cpp
  ${QMLSHARP_NATIVE_TEST_CORE_SOURCES}
  ${QMLSHARP_NATIVE_TEST_FIXTURE_SOURCES}
)

qmlsharp_configure_native_test_target(qmlsharp_native_tests)

set_target_properties(qmlsharp_native_tests
  PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin
)

add_test(NAME qmlsharp_native_tests COMMAND qmlsharp_native_tests)
set_tests_properties(qmlsharp_native_tests PROPERTIES TIMEOUT 30 LABELS "RequiresQt;Native")

if(WIN32 AND DEFINED ENV{QT_DIR} AND NOT "$ENV{QT_DIR}" STREQUAL "")
  set_tests_properties(qmlsharp_native_tests
    PROPERTIES
      ENVIRONMENT_MODIFICATION "PATH=path_list_prepend:$ENV{QT_DIR}/bin"
  )
endif()

add_executable(qmlsharp_native_performance_tests
  native_host_performance_tests.cpp
  ${QMLSHARP_NATIVE_TEST_CORE_SOURCES}
  ${QMLSHARP_NATIVE_TEST_FIXTURE_SOURCES}
)

qmlsharp_configure_native_test_target(qmlsharp_native_performance_tests)

set_target_properties(qmlsharp_native_performance_tests
  PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin
)

add_test(NAME qmlsharp_native_performance_tests COMMAND qmlsharp_native_performance_tests)
set_tests_properties(qmlsharp_native_performance_tests PROPERTIES TIMEOUT 120 LABELS "RequiresQt;Native;Performance")

if(WIN32 AND DEFINED ENV{QT_DIR} AND NOT "$ENV{QT_DIR}" STREQUAL "")
  set_tests_properties(qmlsharp_native_performance_tests
    PROPERTIES
      ENVIRONMENT_MODIFICATION "PATH=path_list_prepend:$ENV{QT_DIR}/bin"
  )
endif()

add_library(qmlsharp_native_integration_fixture SHARED
  ${QMLSHARP_NATIVE_TEST_CORE_SOURCES}
  ${QMLSHARP_NATIVE_TEST_FIXTURE_SOURCES}
  ${QMLSHARP_NATIVE_TEST_INTEGRATION_EXPORT_SOURCE}
)

qmlsharp_configure_native_test_target(qmlsharp_native_integration_fixture)

set_target_properties(qmlsharp_native_integration_fixture
  PROPERTIES
    OUTPUT_NAME qmlsharp_native
    RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin/integration
    LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin/integration
    ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib/integration
)
