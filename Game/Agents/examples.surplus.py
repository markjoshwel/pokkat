"""
surplus: Google Maps Plus Code to iOS Shortcuts-like shareable text
-------------------------------------------------------------------
by mark <mark@joshwel.co> and contributors

This is free and unencumbered software released into the public domain.

Anyone is free to copy, modify, publish, use, compile, sell, or
distribute this software, either in source code form or as a compiled
binary, for any purpose, commercial or non-commercial, and by any
means.

In jurisdictions that recognize copyright laws, the author or authors
of this software dedicate any and all copyright interest in the
software to the public domain. We make this dedication for the benefit
of the public at large and to the detriment of our heirs and
successors. We intend this dedication to be an overt act of
relinquishment in perpetuity of all present and future rights to this
software under copyright law.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR
OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
OTHER DEALINGS IN THE SOFTWARE.

For more information, please refer to <http://unlicense.org/>
"""

from argparse import ArgumentParser
from collections import OrderedDict
from collections.abc import Callable, Sequence
from copy import deepcopy
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from enum import Enum
from functools import lru_cache
from hashlib import shake_256
from json import loads as json_loads
from json.decoder import JSONDecodeError
from platform import platform
from socket import gethostname
from sys import exit as sysexit
from sys import stderr, stdin, stdout
from typing import (
    TYPE_CHECKING,
    Any,
    Final,
    Generic,
    NamedTuple,
    Protocol,
    TextIO,
    TypeAlias,
    TypeVar,
)
from uuid import getnode

from geopy.extra.rate_limiter import RateLimiter as _geopy_RateLimiter  # type: ignore
from geopy.geocoders import Nominatim as _geopy_Nominatim  # type: ignore
from pluscodes import PlusCode as _PlusCode  # type: ignore
from pluscodes import encode as _encode  # type: ignore
from pluscodes.openlocationcode import recoverNearest as _PlusCode_recoverNearest  # type: ignore
from pluscodes.validator import Validator as _PlusCode_Validator  # type: ignore

if TYPE_CHECKING:
    from geopy import Location as _geopy_Location  # type: ignore

# constants

__version__ = "2024.0.0-beta"
VERSION: Final[tuple[int, int, int]] = (2024, 0, 0)
VERSION_SUFFIX: Final[str] = "-beta-local"
BUILD_BRANCH: Final[str] = "future"
BUILD_COMMIT: Final[str] = "latest"
BUILD_DATETIME: Final[datetime] = datetime.now(timezone(timedelta(hours=8)))  # using SGT
CONNECTION_MAX_RETRIES: int = 9
CONNECTION_WAIT_SECONDS: int = 10
LOCALITY_GEOCODER_LEVEL: int = 13  # adjusts geocoder zoom level when
                                   # geocoding lat long into an address

# default shareable text line keys
SHAREABLE_TEXT_LINE_0_KEYS: dict[str, tuple[str, ...]] = {
    "default": (
        "emergency",
        "historic",
        "military",
        "natural",
        "landuse",
        "place",
        "railway",
        "man_made",
        "aerialway",
        "boundary",
        "amenity",
        "aeroway",
        "club",
        "craft",
        "leisure",
        "office",
        "mountain_pass",
        "shop",
        "tourism",
        "bridge",
        "tunnel",
        "waterway",
    ),
}
SHAREABLE_TEXT_LINE_1_KEYS: dict[str, tuple[str, ...]] = {
    "default": ("building",),
}
SHAREABLE_TEXT_LINE_2_KEYS: dict[str, tuple[str, ...]] = {
    "default": ("highway",),
}
SHAREABLE_TEXT_LINE_3_KEYS: dict[str, tuple[str, ...]] = {
    "default": (
        "house_number",
        "house_name",
        "road",
    ),
}
SHAREABLE_TEXT_LINE_4_KEYS: dict[str, tuple[str, ...]] = {
    "default": (
        "residential",
        "neighbourhood",
        "allotments",
        "quarter",
        "city_district",
        "district",
        "borough",
        "suburb",
        "subdivision",
        "municipality",
        "city",
        "town",
        "village",
    ),
}
SHAREABLE_TEXT_LINE_5_KEYS: dict[str, tuple[str, ...]] = {
    "default": ("postcode",),
}
SHAREABLE_TEXT_LINE_6_KEYS: dict[str, tuple[str, ...]] = {
    "default": (
        "region",
        "county",
        "state",
        "state_district",
        "country",
        "continent",
    ),
}
SHAREABLE_TEXT_LINE_SETTINGS: dict[str, dict[int, tuple[str, bool]]] = {
    # line number: (string separator to use, whether to check for seen names)
    "default": {
        0: (", ", False),
        1: (", ", False),
        2: (", ", False),
        3: (" ", False),
        4: (", ", True),
        5: (", ", False),
        6: (", ", False),
    },
}
SHAREABLE_TEXT_NAMES: dict[str, tuple[str, ...]] = {
    "default": (
        SHAREABLE_TEXT_LINE_0_KEYS["default"]
        + SHAREABLE_TEXT_LINE_1_KEYS["default"]
        + SHAREABLE_TEXT_LINE_2_KEYS["default"]
        + ("house_name", "road")
    ),
}
SHAREABLE_TEXT_LOCALITY: dict[str, tuple[str, ...]] = {
    "default": (
        "city_district",
        "district",
        "city",
        *SHAREABLE_TEXT_LINE_6_KEYS["default"],
    ),
}
SHAREABLE_TEXT_DEFAULT: Final[str] = "default"

# special per-country key arrangements for SG/Singapore
SHAREABLE_TEXT_LOCALITY.update(
    {
        "SG": ("country",),
    }
)

# special per-country key arrangements for IT/Italy
SHAREABLE_TEXT_LINE_3_KEYS.update(
    {
        "IT": (
            "road",
            "house_number",
            "house_name",
        ),
    }
)
SHAREABLE_TEXT_LINE_5_KEYS.update(
    {
        "IT": (
            "postcode",
            "region",
            "county",
            "state",
            "state_district",
        ),
    },
)
SHAREABLE_TEXT_LINE_6_KEYS.update(
    {
        "IT": (
            "country",
            "continent",
        ),
    }
)
SHAREABLE_TEXT_LINE_SETTINGS.update({"IT": deepcopy(SHAREABLE_TEXT_LINE_SETTINGS)["default"]})
SHAREABLE_TEXT_LINE_SETTINGS["IT"][5] = (" ", False)

# special per-country key arrangements for MY/Malaysia
SHAREABLE_TEXT_LINE_4_KEYS.update(
    {
        "MY": (),
    },
)
SHAREABLE_TEXT_LINE_5_KEYS.update(
    {
        "MY": (
            "postcode",
            *SHAREABLE_TEXT_LINE_4_KEYS["default"],
        ),
    },
)
SHAREABLE_TEXT_LINE_SETTINGS.update({"MY": deepcopy(SHAREABLE_TEXT_LINE_SETTINGS)["default"]})
SHAREABLE_TEXT_LINE_SETTINGS["MY"][4] = (" ", False)
SHAREABLE_TEXT_LINE_SETTINGS["MY"][5] = (" ", True)


# exceptions


class SurplusError(Exception):
    """base skeleton exception for handling and typing surplus exception classes"""


class NoSuitableLocationError(SurplusError): ...


class IncompletePlusCodeError(SurplusError): ...


class PlusCodeNotFoundError(SurplusError): ...


class LatlongParseError(SurplusError): ...


class EmptyQueryError(SurplusError): ...


# data structures


class TextGenerationEnum(Enum):
    """
    (internal use) enum representing what type of text to generate for _generate_text()

    values
        SHAREABLE_TEXT: str = "sharetext"
        LOCAL_CODE: str = "localcode"
    """

    SHAREABLE_TEXT: str = "sharetext"
    LOCALITY_TEXT: str = "locality_text"


class ConversionResultTypeEnum(Enum):
    """
    enum representing what the result type of conversion should be

    values
        PLUS_CODE: str = "pluscode"
        LOCAL_CODE: str = "localcode"
        LATLONG: str = "latlong"
        SHAREABLE_TEXT: str = "sharetext"
    """

    PLUS_CODE = "pluscode"
    LOCAL_CODE = "localcode"
    LATLONG = "latlong"
    SHAREABLE_TEXT = "sharetext"


ResultType = TypeVar("ResultType")


class Result(NamedTuple, Generic[ResultType]):
    """
    typing.NamedTuple representing a result for safe value retrieval

    arguments
        value: ResultType
            value to return or fallback value if erroneous
        error: BaseException | None = None
            exception if any

    methods
        def __bool__(self) -> bool: ...
        def get(self) -> ResultType: ...
        def cry(self, string: bool = False) -> str: ...

    example
        # do something
        def some_operation(path) -> Result[str]:
            try:
                file = open(path)
                contents = file.read()

            except Exception as exc:
                # must pass a default value
                return Result[str]("", error=exc)

            else:
                return Result[str](contents)

        # call function and handle result
        result = some_operation("some_file.txt")

        if not result:  # check if the result is erroneous
            # .cry() raises the exception
            # (or returns it as a string error message using string=True)
            result.cry()
            ...

        else:
            # .get() raises exception or returns value,
            # but since we checked for errors this is safe
            print(result.get())
    """

    value: ResultType
    error: BaseException | None = None

    def __bool__(self) -> bool:
        """method that returns True if self.error is not None"""
        return self.error is None

    def cry(self, string: bool = False) -> str:  # noqa: FBT001, FBT002
        """
        method that raises self.error if is an instance of BaseException,
        returns self.error if is an instance of str, or returns an empty string if
        self.error is None

        arguments
            string: bool = False
                if self.error is an Exception, returns it as a string error message
        """

        if isinstance(self.error, BaseException):
            if string:
                message = f"{self.error}"
                name = self.error.__class__.__name__
                return f"{message} ({name})" if (message != "") else name

            raise self.error

        if isinstance(self.error, str):
            return self.error

        return ""

    def get(self) -> ResultType:
        """method that returns self.value if Result is non-erroneous else raises error"""
        if isinstance(self.error, BaseException):
            raise self.error
        return self.value


class Latlong(NamedTuple):
    """
    typing.NamedTuple representing a latitude-longitude coordinate pair and any extra
    information

    arguments
        latitude: float
        longitude: float
        bounding_box: tuple[float, float, float, float] | None = None
            a four-tuple representing a bounding box, (lat1, lat2, lon1, lon2) or None
            the user does not need to enter this. this attribute is only used for
            shortening plus codes, and will be supplied by the geocoding service.

    methods
        def __str__(self) -> str: ...
    """

    latitude: float
    longitude: float
    bounding_box: tuple[float, float, float, float] | None = None

    def __str__(self) -> str:
        """
        method that returns a comma-and-space-seperated string of self.latitude and
        self.longitude
        """
        return f"{self.latitude}, {self.longitude}"


EMPTY_LATLONG: Final[Latlong] = Latlong(latitude=0.0, longitude=0.0)


class SurplusGeocoderProtocol(Protocol):
    """
    typing_extensions.Protocol class for documentation and static type checking of
    surplus reverser functions

        (place: str) -> Latlong

    name string to location function. must take in a string and return a Latlong.

    **the function returned MUST supply a `bounding_box` attribute to the to-be-returned
    [Latlong](#class-latlong).** the bounding box is used when surplus shortens Plus Codes.

    function can and should be at minimum functools.lru_cache()-wrapped if the geocoding
    service asks for caching

    exceptions are handled by the caller
    """

    def __call__(self, place: str) -> Latlong: ...


class SurplusReverserProtocol(Protocol):
    """
    typing_extensions.Protocol class for documentation and static type checking of
    surplus reverser functions

        (latlong: Latlong, level: int = 18) -> dict[str, Any]:

    Latlong object to address information dictionary function. must take in a string and
    return a dict with SHAREABLE_TEXT_LINE_*_KEYS keys at the dictionaries' top-level.
    keys are used to access address information.

    function should also take in a int representing the level of detail for the
    returned address, 0-18 (country-level to building), inclusive.

    keys for latitude, longitude and an iso3166-2 (or closest equivalent) should also be
    included at the dictionaries top level as the keys `latitude`, `longitude` and
    `ISO3166-2` (non-case sensitive, or at least something starting with `ISO3166`)
    respectively.

        {
            'ISO3166-2-lvl6': 'SG-03',
            'amenity': 'Ngee Ann Polytechnic',
            ...
            'country': 'Singapore',
            'latitude': 1.33318835,
            'longitude': 103.77461234638255,
            'postcode': '599489',
            'raw': {...},
        }

    function can and should be at minimum functools.lru_cache()-wrapped if the geocoding
    service asks for caching

    exceptions are handled by the caller
    """

    def __call__(self, latlong: Latlong, level: int = 18) -> dict[str, Any]: ...


class PlusCodeQuery(NamedTuple):
    """
    typing.NamedTuple representing a full-length Plus Code (e.g., 6PH58QMF+FX)

    arguments
        code: str

    methods
        def to_lat_long_coord(self, ...) -> Result[Latlong]: ...
        def __str__(self) -> str: ...
    """

    code: str

    def to_lat_long_coord(self, geocoder: SurplusGeocoderProtocol) -> Result[Latlong]:  # noqa: ARG002
        """
        method that returns a latitude-longitude coordinate pair

        arguments
            geocoder: SurplusGeocoderProtocol
                name string to location function, see SurplusGeocoderProtocol docstring
                for more information

        returns Result[Latlong]
        """

        latitude: float = 0.0
        longitude: float = 0.0

        try:
            plus_code = _PlusCode(self.code)
            latitude = plus_code.area.center().lat
            longitude = plus_code.area.center().lon

        except KeyError:
            return Result[Latlong](
                EMPTY_LATLONG,
                error=IncompletePlusCodeError(
                    "PlusCodeQuery.to_lat_long_coord: "
                    "Plus Code is not full-length (e.g., 6PH58QMF+FX)"
                ),
            )

        except Exception as exc:  # noqa: BLE001
            return Result[Latlong](EMPTY_LATLONG, error=exc)

        return Result[Latlong](Latlong(latitude=latitude, longitude=longitude))

    def __str__(self) -> str:
        """method that returns string representation of query"""
        return f"{self.code}"


class LocalCodeQuery(NamedTuple):
    """
    typing.NamedTuple representing a shortened Plus Code with locality, referred to by
    surplus as a "local code"

    arguments
        code: str
            Plus Code portion of local code, e.g., "8QMF+FX"
        locality: str
            remaining string of local code, e.g., "Singapore"

    methods
        def to_full_plus_code(self, ...) -> Result[str]: ...
        def to_lat_long_coord(self, ...) -> Result[Latlong]: ...
        def __str__(self) -> str: ...
    """

    code: str
    locality: str

    def to_full_plus_code(self, geocoder: SurplusGeocoderProtocol) -> Result[str]:
        """
        exclusive method that returns a full-length Plus Code as a string

        arguments
            geocoder: SurplusGeocoderProtocol
                name string to location function, see SurplusGeocoderProtocol docstring
                for more information

        returns Result[str]
        """

        try:
            locality_location = geocoder(self.locality)

            recovered_pluscode = _PlusCode_recoverNearest(
                code=self.code,
                referenceLatitude=locality_location.latitude,
                referenceLongitude=locality_location.longitude,
            )

            return Result[str](recovered_pluscode)

        except Exception as exc:  # noqa: BLE001
            return Result[str]("", error=exc)

    def to_lat_long_coord(self, geocoder: SurplusGeocoderProtocol) -> Result[Latlong]:
        """
        method that returns a latitude-longitude coordinate pair

        arguments
            geocoder: SurplusGeocoderProtocol
                name string to location function, see SurplusGeocoderProtocol docstring
                for more information

        returns Result[Latlong]
        """

        recovered_pluscode = self.to_full_plus_code(geocoder=geocoder)

        if not recovered_pluscode:
            return Result[Latlong](EMPTY_LATLONG, error=recovered_pluscode.error)

        return Result[Latlong](
            PlusCodeQuery(recovered_pluscode.get())
            .to_lat_long_coord(geocoder=geocoder)
            .get()  # PlusCodeQuery can get latlong coord safely, so no need to handle
        )

    def __str__(self) -> str:
        """method that returns string representation of query"""
        return f"{self.code} {self.locality}"


class LatlongQuery(NamedTuple):
    """
    typing.NamedTuple representing a latitude-longitude coordinate pair

    arguments
        latlong: Latlong

    methods
        def to_lat_long_coord(self, ...) -> Result[Latlong]: ...
        def __str__(self) -> str: ...
    """

    latlong: Latlong

    def to_lat_long_coord(self, geocoder: SurplusGeocoderProtocol) -> Result[Latlong]:  # noqa: ARG002
        """
        method that returns a latitude-longitude coordinate pair

        arguments
            geocoder: SurplusGeocoderProtocol
                name string to location function, see SurplusGeocoderProtocol docstring
                for more information

        returns Result[Latlong]
        """

        return Result[Latlong](self.latlong)

    def __str__(self) -> str:
        """method that returns string representation of query"""
        return f"{self.latlong!s}"


class StringQuery(NamedTuple):
    """
    typing.NamedTuple representing a pure string query

    arguments
        query: str

    methods
        def to_lat_long_coord(self, ...) -> Result[Latlong]: ...
        def __str__(self) -> str: ...
    """

    query: str

    def to_lat_long_coord(self, geocoder: SurplusGeocoderProtocol) -> Result[Latlong]:
        """
        method that returns a latitude-longitude coordinate pair

        arguments
            geocoder: SurplusGeocoderProtocol
                name string to location function, see SurplusGeocoderProtocol docstring
                for more information

        returns Result[Latlong]
        """

        try:
            return Result[Latlong](geocoder(self.query))

        except Exception as exc:  # noqa: BLE001
            return Result[Latlong](EMPTY_LATLONG, error=exc)

    def __str__(self) -> str:
        """method that returns string representation of query"""
        return self.query


Query: TypeAlias = PlusCodeQuery | LocalCodeQuery | LatlongQuery | StringQuery


def generate_fingerprinted_user_agent() -> Result[str]:
    """
    function that attempts to return a unique user agent string.

    returns Result[str]
        this result will always have a valid value as erroneous results will have a
        resulting value of 'surplus/<version> (generic-user)'

        valid results will have a value of 'surplus/<version> (<fingerprint hash>)',
        where <fingerprint hash> is a 12 character hexadecimal string
    """
    version: str = ".".join([str(v) for v in VERSION]) + VERSION_SUFFIX

    def _try(func: Callable) -> str:
        try:
            return func()

        except Exception:  # noqa: BLE001
            return "unknown"

    system_info: str = _try(platform)
    hostname = _try(gethostname)
    mac_address = _try(
        lambda: ":".join(
            [f"{(getnode() >> elements) & 0xFF:02x}" for elements in range(0, 2 * 6, 2)][::-1]
        )
    )
    unique_info = f"{version}-{system_info}-{hostname}-{mac_address}"

    if unique_info == "unknown-unknown-unknown-unknown":
        return Result[str](f"surplus/{version} (generic-user)")

    fingerprint: str = shake_256(unique_info.encode()).hexdigest(6)
    return Result[str](f"surplus/{version} ({fingerprint})")


default_fingerprint: Final[str] = generate_fingerprinted_user_agent().value


@dataclass
class SurplusDefaultGeocoding:
    """
    dataclass providing the default geocoding functionality for surplus, via
    OpenStreetMap Nominatim

    attributes
        user_agent: str = default_fingerprint
            pass in a custom user agent here, else it will be the default fingerprinted
            user agent

    usage
        geocoding = SurplusDefaultGeocoding(behaviour.user_agent)
        geocoding.update_geocoding_functions()
        ...
        Behaviour(
            ...,
            geocoder=geocoding.geocoder,
            reverser=geocoding.reverser
        )
    """

    user_agent: str = default_fingerprint
    _ratelimited_raw_geocoder: Callable = lambda _: None  # noqa: E731
    _ratelimited_raw_reverser: Callable = lambda _: None  # noqa: E731
    _first_update: bool = False

    def update_geocoding_functions(self) -> None:
        """
        re-initialise the geocoding functions with the current user agent, also generate
        a new user agent if not set properly
        """

        if not isinstance(self.user_agent, str):
            self.user_agent: str = generate_fingerprinted_user_agent().value

        nominatim = _geopy_Nominatim(user_agent=self.user_agent)

        # this is

        self._ratelimited_raw_geocoder: Callable = lru_cache(
            _geopy_RateLimiter(
                nominatim.geocode,
                max_retries=CONNECTION_MAX_RETRIES,
                error_wait_seconds=CONNECTION_WAIT_SECONDS,
            )
        )

        self._ratelimited_raw_reverser: Callable = lru_cache(
            _geopy_RateLimiter(
                nominatim.reverse,
                max_retries=CONNECTION_MAX_RETRIES,
                error_wait_seconds=CONNECTION_WAIT_SECONDS,
            )
        )

        self._first_update = True

    def geocoder(self, place: str) -> Latlong:
        """
        default geocoder for surplus, uses OpenStreetMap Nominatim

        see SurplusGeocoderProtocol for more information on surplus geocoder functions
        """

        if self._first_update is False:
            self.update_geocoding_functions()

        location: _geopy_Location | None = self._ratelimited_raw_geocoder(place)

        if location is None:
            msg = f"No suitable location could be geolocated from '{place}'"
            raise NoSuitableLocationError(msg)

        bounding_box: tuple[float, float, float, float] | None = location.raw.get(
            "boundingbox", None
        )

        if location.raw.get("boundingbox", None) is not None:
            _bounding_box = [float(c) for c in location.raw.get("boundingbox", [])]
            if len(_bounding_box) == 4:  # noqa: PLR2004
                bounding_box = (
                    _bounding_box[0],
                    _bounding_box[1],
                    _bounding_box[2],
                    _bounding_box[3],
                )

        return Latlong(
            latitude=location.latitude,
            longitude=location.longitude,
            bounding_box=bounding_box,
        )

    def reverser(self, latlong: Latlong, level: int = 18) -> dict[str, Any]:
        """
        default reverser for surplus, uses OpenStreetMap Nominatim

        arguments
            latlong: Latlong
            level: int = 0
                level of detail for the returned address, 0-18 (country-building) inclusive

        see SurplusReverserProtocol for more information on surplus reverser functions
        """

        if self._first_update is False:
            self.update_geocoding_functions()

        location: _geopy_Location | None = self._ratelimited_raw_reverser(str(latlong), zoom=level)

        if location is None:
            msg = f"could not reverse '{latlong!s}'"
            raise NoSuitableLocationError(msg)

        location_dict: dict[str, Any] = {}

        for key in (address := location.raw.get("address", {})):
            location_dict[key] = address.get(key, "")

        location_dict["raw"] = location.raw
        location_dict["latitude"] = location.latitude
        location_dict["longitude"] = location.longitude

        return location_dict


default_geocoding: Final[SurplusDefaultGeocoding] = SurplusDefaultGeocoding(default_fingerprint)


class Behaviour(NamedTuple):
    """
    typing.NamedTuple representing how surplus operations should behave

    arguments
        query: str | list[str] = ""
            original user-passed query string or a list of strings from splitting
            user-passed query string by spaces
        geocoder: SurplusGeocoderProtocol = default_geocoding.geocoder
            name string to location function, see SurplusGeocoderProtocol docstring for
            for more information
        reverser: SurplusReverserProtocol = default_geocoding.reverser
            latlong to address information dict function, see SurplusReverserProtocol
            docstring for more information
        stderr: TextIO = sys.stderr
            TextIO-like object representing a writeable file. defaults to sys.stderr
        stdout: TextIO = sys.stdout
            TextIO-like object representing a writeable file. defaults to sys.stdout
        debug: bool = False
            whether to print debug information to stderr
        version_header: bool = False
            whether to print version information and exit
        convert_to_type: ConversionResultTypeEnum = ConversionResultTypeEnum.SHAREABLE_TEXT
            what type to convert query to
        using_termux_location: bool = False
            treats query as a termux-location output json string, and parses it accordingly
        show_user_agent: bool = False
            whether to print the fingerprinted user agent and exit
    """

    query: str | list[str] = ""
    geocoder: SurplusGeocoderProtocol = default_geocoding.geocoder
    reverser: SurplusReverserProtocol = default_geocoding.reverser
    stderr: TextIO = stderr
    stdout: TextIO = stdout
    debug: bool = False
    version_header: bool = False
    convert_to_type: ConversionResultTypeEnum = ConversionResultTypeEnum.SHAREABLE_TEXT
    using_termux_location: bool = False
    show_user_agent: bool = False


# functions


def parse_query(behaviour: Behaviour) -> Result[Query]:
    """
    function that parses a query string into a query object

    arguments
        behaviour: Behaviour

    returns Result[Query]
    """

    def _match_plus_code(
        behaviour: Behaviour,
    ) -> Result[Query]:
        """
        internal helper code reuse function

        looks through each 'word' and attempts to match to a Plus Code
        if found, remove from original query and strip of whitespace and commas
        use resulting stripped query as locality
        """

        validator = _PlusCode_Validator()
        portion_plus_code: str = ""
        portion_locality: str = ""
        original_query: str = ""
        split_query: list[str] = []

        if isinstance(behaviour.query, list):
            original_query = " ".join(behaviour.query)
            split_query = behaviour.query

        else:
            original_query = str(behaviour.query)
            split_query = behaviour.query.split(" ")

        for _word in split_query:
            word = _word.strip(",").strip()

            if validator.is_valid(word):
                portion_plus_code = word

                if validator.is_full(word):
                    return Result[Query](PlusCodeQuery(portion_plus_code))

                break

        # didn't find a plus code. :(
        if portion_plus_code == "":
            return Result[Query](
                LatlongQuery(EMPTY_LATLONG),
                error=PlusCodeNotFoundError("unable to find a Plus Code"),
            )

        # found a plus code!
        portion_locality = original_query.replace(portion_plus_code, "")
        portion_locality = portion_locality.strip().strip(",").strip()

        # did find plus code, but not full-length. :(
        if (portion_locality == "") and (not validator.is_full(portion_plus_code)):
            return Result[Query](
                LatlongQuery(EMPTY_LATLONG),
                error=IncompletePlusCodeError(
                    "_match_plus_code: Plus Code is not full-length (e.g., 6PH58QMF+FX)"
                ),
            )

        if behaviour.debug:
            print(
                f"debug: _match_plus_code: {portion_plus_code=}, {portion_locality=}",
                file=behaviour.stderr,
            )

        return Result[Query](
            LocalCodeQuery(
                code=portion_plus_code,
                locality=portion_locality,
            )
        )

    # types to handle:
    #
    # plus codes
    #   6PH58R3M+F8
    # local codes
    #   8RQQ+4Q Singapore                        (single-word-long locality suffix)
    #   St Lucia, Queensland, Australia G227+XF  (multi-word-long locality prefix)
    # latlong coords
    #   1.3521,103.8198   (single-word-long with comma)
    #   1.3521, 103.8198  (space-seperated with comma)
    #   1.3521 103.8198   (space-seperated without comma)
    # string queries
    #   Ngee Ann Polytechnic, Singapore  (has a comma)
    #   Toa Payoh North                  (no commas)

    if behaviour.debug:
        print(f"debug: parse_query: {behaviour.query=}", file=behaviour.stderr)

    # check if empty
    if behaviour.query in ([], ""):
        return Result[Query](
            LatlongQuery(EMPTY_LATLONG),
            error=EmptyQueryError("empty query string passed"),
        )

    # try to find a plus/local code
    if mpc_result := _match_plus_code(behaviour=behaviour):
        # found one!
        return Result[Query](mpc_result.get())

    # is a plus/local code, but missing details
    if isinstance(mpc_result.error, IncompletePlusCodeError):
        return mpc_result  # propagate back up to caller

    # handle query
    original_query: str = ""
    split_query: list[str] = []

    if isinstance(behaviour.query, str):
        original_query = behaviour.query
        split_query = behaviour.query.split(" ")

    else:
        original_query = " ".join(behaviour.query)
        split_query = behaviour.query

    if behaviour.debug:
        print(
            f"debug: parse_query: {split_query=}\n",
            f"debug: parse_query: {original_query=}",
            sep="",
            file=behaviour.stderr,
        )

    # check if termux-location json
    if behaviour.using_termux_location:
        try:
            termux_location_json = json_loads(original_query)
            if not isinstance(termux_location_json, dict):
                msg = "parsed termux-location json is not a dict"
                raise TypeError(msg)  # noqa: TRY301

            return Result[Query](
                LatlongQuery(
                    Latlong(
                        latitude=termux_location_json["latitude"],
                        longitude=termux_location_json["longitude"],
                    )
                )
            )

        except (JSONDecodeError, TypeError):
            return Result[Query](
                LatlongQuery(EMPTY_LATLONG),
                error=ValueError("could not parse termux-location json"),
            )

        except KeyError:
            return Result[Query](
                LatlongQuery(EMPTY_LATLONG),
                error=ValueError(
                    "could not get 'latitude' or 'longitude' keys from termux-location json"
                ),
            )

        except Exception as exc:  # noqa: BLE001
            return Result[Query](
                LatlongQuery(EMPTY_LATLONG),
                error=exc,
            )

        # unreachable

    # not a plus/local code/termux-location json,
    # try to match for latlong or string query
    match split_query:
        case [single]:
            # possibly a:
            #   comma-seperated single-word-long latlong coord
            #   (fallback) single word string query

            if "," not in single:  # no comma, not a latlong coord
                return Result[Query](StringQuery(original_query))

            # has comma, possibly a latlong coord
            comma_split_single: list[str] = single.split(",")

            if len(comma_split_single) == 2:  # noqa: PLR2004
                try:  # try to type cast query
                    latitude = float(comma_split_single[0].strip(","))
                    longitude = float(comma_split_single[-1].strip(","))

                except ValueError:  # not a latlong coord, fallback
                    return Result[Query](StringQuery(single))

                else:  # are floats, so is a latlong coord
                    return Result[Query](
                        LatlongQuery(
                            Latlong(
                                latitude=latitude,
                                longitude=longitude,
                            )
                        )
                    )

            # not a latlong coord, fallback
            return Result[Query](StringQuery(original_query))

        case [left_single, right_single]:
            # possibly a:
            #   space-seperated latlong coord
            #   (fallback) space-seperated string query

            try:  # try to type cast query
                latitude = float(left_single.strip(","))
                longitude = float(right_single.strip(","))

            except ValueError:  # not a latlong coord, fallback
                return Result[Query](StringQuery(original_query))

            else:  # are floats, so is a latlong coord
                return Result[Query](LatlongQuery(Latlong(latitude=latitude, longitude=longitude)))

        case _:
            # possibly a:
            #   (fallback) space-seperated string query

            return Result[Query](StringQuery(original_query))


def handle_args() -> Behaviour:
    """
    internal function that handles command-line arguments

    returns Behaviour
        program behaviour namedtuple
    """

    parser = ArgumentParser(
        prog="surplus",
        description=__doc__[__doc__.find(":") + 2 : __doc__.find("\n", 1)],
    )
    parser.add_argument(
        "query",
        type=str,
        help=(
            "full-length Plus Code (6PH58QMF+FX), "
            "shortened Plus Code/'local code' (8QMF+FX Singapore), "
            "latlong (1.3336875, 103.7749375), "
            "string query (e.g., 'Wisma Atria'), "
            "or '-' to read from stdin"
        ),
        nargs="*",
    )
    parser.add_argument(
        "-d",
        "--debug",
        action="store_true",
        default=False,
        help="prints lat, long and reverser response dict to stderr",
    )
    parser.add_argument(
        "-v",
        "--version",
        action="store_true",
        default=False,
        help="prints version information to stderr and exits",
    )
    (
        parser.add_argument(
            "-c",
            "--convert-to",
            type=str,
            choices=[str(v.value) for v in ConversionResultTypeEnum],
            help=(
                "converts query a specific output type, defaults to "
                f"'{Behaviour([]).convert_to_type.value}'"
            ),
            default=Behaviour([]).convert_to_type.value,
        ),
    )
    parser.add_argument(
        "-u",
        "--user-agent",
        type=str,
        help=(
            "user agent string to use for geocoding service, "
            "defaults to fingerprinted user agent string"
        ),
        default=default_fingerprint,
    )
    parser.add_argument(
        "--show-user-agent",
        action="store_true",
        default=False,
        help="prints fingerprinted user agent string and exits",
    )
    parser.add_argument(
        "-t",
        "--using-termux-location",
        action="store_true",
        default=False,
        help="treats input as a termux-location output json string, and parses it accordingly",
    )

    # initialisation
    args = parser.parse_args()
    query: str | list[str] = ""

    # "-" stdin check
    query = "\n".join([line.strip() for line in stdin]) if (args.query == ["-"]) else args.query

    # setup structures and return
    geocoding = SurplusDefaultGeocoding(args.user_agent)
    return Behaviour(
        query=query,
        geocoder=geocoding.geocoder,
        reverser=geocoding.reverser,
        stderr=stderr,
        stdout=stdout,
        debug=args.debug,
        version_header=args.version,
        convert_to_type=ConversionResultTypeEnum(args.convert_to),
        using_termux_location=args.using_termux_location,
        show_user_agent=args.show_user_agent,
    )


def _unique(container: Sequence[str]) -> list[str]:
    """(internal function) returns a in-order unique list from list"""
    unique: OrderedDict = OrderedDict()
    for line in container:
        unique.update({line: None})
    return list(unique.keys())


def _generate_text(
    location: dict[str, Any],
    behaviour: Behaviour,
    mode: TextGenerationEnum = TextGenerationEnum.SHAREABLE_TEXT,
    debug: bool = False,  # noqa: FBT001, FBT002
) -> str:
    """
    (internal function) generate shareable text from location dict

    arguments
        location: dict[str, Any]
            dictionary from geocoding reverser function
        behaviour: Behaviour
            surplus behaviour
        mode: GenerationModeEnum = GenerationModeEnum.SHAREABLE_TEXT
                generation mode, defaults to shareable text generation
        debug: bool = False
            behaviour-seperate debug flag because this function is called twice by
            surplus in debug mode, one for debug and one for non-debug output

    returns str
    """

    def _generate_text_line(
        line_number: int,
        line_keys: Sequence[str],
        separator: str = ", ",
        filter_func: Callable[[str], list[bool]] = lambda _: [True],
    ) -> str:
        """
        (internal function) generate a line of shareable text from a list of keys

        arguments
            line_number: int
                line number to prefix with
            line_keys: Sequence[str]
                list of keys to .get() from location dict
            separator: str = ", "
                separator to join elements with
            filter_func: Callable[[str], list[bool]] = lambda e: True
                function that takes in a string and returns a list of bools, used to
                filter elements from line_keys. list will be passed to all(). if all
                returns True, then the element is kept.

        returns str
        """

        line_prefix: str = f"{line_number}\t" if debug else ""
        basket: list[str] = []

        for detail in _unique([str(location.get(detail, "")) for detail in line_keys]):
            if detail == "":
                continue

            # filtering: if all(filter_func(detail)) returns True,
            #            then the element is kept/added to 'basket'

            if filter_status := all(detail_check := filter_func(detail)) is True:
                if debug:
                    print(
                        "debug: _generate_text_line: "
                        f"{detail_check!s:<20} -> {filter_status!s:<5}  "
                        f"--------  '{detail}'",
                        file=behaviour.stderr,
                    )

                basket.append(detail)

            else:  # filter function returned False, so element is filtered/skipped
                if debug:
                    print(
                        "debug: _generate_text_line: "
                        f"{detail_check!s:<20} -> {filter_status!s:<5}"
                        f"  filtered  '{detail}'",
                        file=behaviour.stderr,
                    )
                continue

        line = line_prefix + separator.join(basket)
        return (line + "\n") if (line != "") else ""

    C = TypeVar("C")

    def stget(split_iso3166_2: list[str], line_keys: dict[str, C]) -> tuple[bool, C]:
        """
        (internal function)

        arguments:
            split_iso3166_2: list[str]
                the dash-split iso 3166-2 country code

            line_keys:
                the shareable text line keys dict to use

        returns tuple[bool, C]
            bool: whether the a special key arrangement was used
            C: dict content
        """

        country: str = "default"
        if len(iso3166_2) >= 1:
            country = split_iso3166_2[0]

        if country not in line_keys:
            return False, line_keys["default"]

        return True, line_keys[country]

    # iso3166-2 handling: this allows surplus to have special key arrangements for a
    #                     specific iso3166-2 code for edge cases
    #                     (https://en.wikipedia.org/wiki/ISO_3166-2)

    # get iso3166-2 before doing anything
    iso3166_2: str = ""
    for key in location:
        if key.lower().startswith("iso3166"):
            iso3166_2 = location.get(key, "")

    split_iso3166_2 = [part.upper() for part in iso3166_2.split("-")]

    if debug:
        print(
            f"debug: _generate_text: {split_iso3166_2=}",
            file=behaviour.stderr,
        )

    n_used_special: int = 0  # number of special key arrangements used

    # skeleton code to allow for changing keys based on iso3166-2 code
    used_special, st_line0_keys = stget(split_iso3166_2, SHAREABLE_TEXT_LINE_0_KEYS)
    n_used_special += used_special

    used_special, st_line1_keys = stget(split_iso3166_2, SHAREABLE_TEXT_LINE_1_KEYS)
    n_used_special += used_special

    used_special, st_line2_keys = stget(split_iso3166_2, SHAREABLE_TEXT_LINE_2_KEYS)
    n_used_special += used_special

    used_special, st_line3_keys = stget(split_iso3166_2, SHAREABLE_TEXT_LINE_3_KEYS)
    n_used_special += used_special

    used_special, st_line4_keys = stget(split_iso3166_2, SHAREABLE_TEXT_LINE_4_KEYS)
    n_used_special += used_special

    used_special, st_line5_keys = stget(split_iso3166_2, SHAREABLE_TEXT_LINE_5_KEYS)
    n_used_special += used_special

    used_special, st_line6_keys = stget(split_iso3166_2, SHAREABLE_TEXT_LINE_6_KEYS)
    n_used_special += used_special

    used_special, st_names = stget(split_iso3166_2, SHAREABLE_TEXT_NAMES)
    n_used_special += used_special

    used_special, st_locality = stget(split_iso3166_2, SHAREABLE_TEXT_LOCALITY)
    n_used_special += used_special

    used_special, st_line_settings = stget(split_iso3166_2, SHAREABLE_TEXT_LINE_SETTINGS)
    n_used_special += used_special

    if n_used_special and debug:
        print(
            "debug: _generate_text: "
            f"using special key arrangements for '{iso3166_2}' (Singapore)",
            file=behaviour.stderr,
        )

    # start generating text
    match mode:
        case TextGenerationEnum.SHAREABLE_TEXT:
            text: list[str] = []

            seen_names: list[str] = [
                detail
                for detail in _unique(
                    [str(location.get(location_key, "")) for location_key in st_names]
                )
                if detail != ""
            ]

            if debug:
                print(f"debug: _generate_text: {seen_names=}", file=behaviour.stderr)

            general_global_info: list[str] = [
                str(location.get(detail, "")) for detail in st_line6_keys
            ]

            for (
                line_number,
                line_keys,
            ) in enumerate(
                [
                    st_line0_keys,
                    st_line1_keys,
                    st_line2_keys,
                    st_line3_keys,
                    st_line4_keys,
                    st_line5_keys,
                    st_line6_keys,
                ]
            ):
                line_separator, line_filter = st_line_settings[line_number]

                # filter: everything here should be True if the element is to be kept
                if line_filter is False:
                    _filter = lambda _: [True]  # noqa: E731
                else:
                    _filter = lambda ak: [  # noqa: E731
                        ak not in general_global_info,
                        not any(ak in sn for sn in seen_names),
                    ]

                text.append(
                    _generate_text_line(
                        line_number=line_number,
                        line_keys=line_keys,
                        separator=line_separator,
                        filter_func=_filter,
                    )
                )

            return "".join(_unique(text)).rstrip()

        case TextGenerationEnum.LOCALITY_TEXT:
            return _generate_text_line(
                line_number=0,
                line_keys=st_locality,
            )

        case _:
            msg = f"unknown mode '{mode}' (expected a TextGenerationEnum)"
            raise NotImplementedError(msg)


def surplus(query: Query | str, behaviour: Behaviour) -> Result[str]:
    """
    query to shareable text conversion function

    query: Query | str
        query object to convert or string to attempt to query for then convert
    behaviour: Behaviour
        surplus behaviour namedtuple

    returns Result[str]
    """

    if not isinstance(query, PlusCodeQuery | LocalCodeQuery | LatlongQuery | StringQuery):
        query_result = parse_query(
            behaviour=Behaviour(
                query=str(query),
                geocoder=behaviour.geocoder,
                reverser=behaviour.reverser,
                stderr=behaviour.stderr,
                stdout=behaviour.stdout,
                debug=behaviour.debug,
                version_header=behaviour.version_header,
                convert_to_type=behaviour.convert_to_type,
            )
        )

        if not query_result:
            return Result[str]("", error=query_result.error)

        query = query_result.get()

    # operate on query
    text: str = ""

    match behaviour.convert_to_type:
        case ConversionResultTypeEnum.SHAREABLE_TEXT:
            # get latlong and handle result
            latlong_result: Result[Latlong] = query.to_lat_long_coord(geocoder=behaviour.geocoder)

            if not latlong_result:
                return Result[str]("", error=latlong_result.error)

            if behaviour.debug:
                print(f"debug: {latlong_result.get()=}", file=behaviour.stderr)

            # reverse location and handle result
            try:
                location = behaviour.reverser(latlong_result.get())

            except Exception as exc:  # noqa: BLE001
                return Result[str]("", error=exc)

            if behaviour.debug:
                print(f"debug: {location=}", file=behaviour.stderr)

            # generate text
            if behaviour.debug:
                print(
                    _generate_text(
                        location=location,
                        behaviour=behaviour,
                        debug=behaviour.debug,
                    ),
                    file=behaviour.stderr,
                )

            text = _generate_text(
                location=location,
                behaviour=behaviour,
            )

            return Result[str](text)

        case ConversionResultTypeEnum.PLUS_CODE:
            # if its already a plus code, just return it
            if isinstance(query, PlusCodeQuery):
                return Result[str](str(query))

            # get latlong and handle result
            latlong_query = query.to_lat_long_coord(geocoder=behaviour.geocoder)

            if not latlong_query:
                return Result[str]("", error=latlong_query.error)

            if behaviour.debug:
                print(f"debug: {latlong_query.get()=}", file=behaviour.stderr)

            # perform operation
            try:
                pluscode: str = _encode(
                    lat=latlong_query.get().latitude, lon=latlong_query.get().longitude
                )

            except Exception as exc:  # noqa: BLE001
                return Result[str]("", error=exc)

            return Result[str](pluscode)

        case ConversionResultTypeEnum.LOCAL_CODE:
            # if its already a local code, just return it
            if isinstance(query, LocalCodeQuery):
                return Result[str](str(query))

            # get latlong and handle result
            latlong_result = query.to_lat_long_coord(geocoder=behaviour.geocoder)

            if not latlong_result:
                return Result[str]("", error=latlong_result.error)

            if behaviour.debug:
                print(f"debug: {latlong_result.get()=}", file=behaviour.stderr)

            query_latlong = latlong_result.get()

            # reverse location and handle result
            try:
                location = behaviour.reverser(query_latlong, level=LOCALITY_GEOCODER_LEVEL)

            except Exception as exc:  # noqa: BLE001
                return Result[str]("", error=exc)

            if behaviour.debug:
                print(f"debug: {location=}", file=behaviour.stderr)

            # generate locality portion of local code
            if behaviour.debug:
                stdout.write(
                    _generate_text(
                        location=location,
                        behaviour=behaviour,
                        mode=TextGenerationEnum.LOCALITY_TEXT,
                        debug=behaviour.debug,
                    ).strip()
                    + "\n"
                )

            portion_locality: str = _generate_text(
                location=location,
                behaviour=behaviour,
                mode=TextGenerationEnum.LOCALITY_TEXT,
            ).strip()

            # reverse locality portion
            try:
                locality_latlong: Latlong = behaviour.geocoder(portion_locality)

                # check now if bounding_box is set and valid
                if getattr(locality_latlong, "bounding_box", None) is None:
                    msg = (
                        "(shortening) geocoder-returned latlong has .bounding_box=None"
                        f" - {locality_latlong.bounding_box}"
                    )
                    raise AttributeError(msg)  # noqa: TRY301

                if len(locality_latlong.bounding_box) != 4:  # noqa: PLR2004
                    msg = (
                        "(shortening) geocoder-returned latlong has len(.bounding_box) != 4"
                        f" - {locality_latlong.bounding_box}"
                    )
                    raise ValueError(msg)  # noqa: TRY301

                if not all(type(c) == float(c) for c in locality_latlong.bounding_box):
                    msg = (
                        "(shortening) geocoder-returned latlong has non-float in .bounding_box"
                        f" - {locality_latlong.bounding_box}"
                    )
                    raise TypeError(msg)  # noqa: TRY301

            except Exception as exc:  # noqa: BLE001
                return Result[str]("", error=exc)

            plus_code = _encode(
                lat=query_latlong.latitude,
                lon=query_latlong.longitude,
            )

            # https://github.com/google/open-location-code/wiki/Guidance-for-shortening-codes
            check1 = (
                # The center point of the feature is within 0.4 degrees latitude and 0.4
                # degrees longitude
                (
                    (query_latlong.latitude - 0.4)
                    <= locality_latlong.latitude
                    <= (query_latlong.latitude + 0.4)
                ),
                (
                    (query_latlong.longitude - 0.4)
                    <= locality_latlong.longitude
                    <= (query_latlong.longitude + 0.4)
                ),
                # The bounding box of the feature is less than 0.8 degrees high and wide.
                abs(locality_latlong.bounding_box[0] - locality_latlong.bounding_box[1]) < 0.8,  # noqa: PLR2004
                abs(locality_latlong.bounding_box[2] - locality_latlong.bounding_box[3]) < 0.8,  # noqa: PLR2004
            )

            check2 = (
                # The center point of the feature is within 0.4 degrees latitude and 0.4
                # degrees longitude"
                (
                    (query_latlong.latitude - 8)
                    <= locality_latlong.latitude
                    <= (query_latlong.latitude + 8)
                ),
                (
                    (query_latlong.longitude - 8)
                    <= locality_latlong.longitude
                    <= (query_latlong.longitude + 8)
                ),
                # The bounding box of the feature is less than 0.8 degrees high and wide.
                abs(locality_latlong.bounding_box[0] - locality_latlong.bounding_box[1]) < 16,  # noqa: PLR2004
                abs(locality_latlong.bounding_box[2] - locality_latlong.bounding_box[3]) < 16,  # noqa: PLR2004
            )

            if check1:
                return Result[str](f"{plus_code[4:]} {portion_locality}")

            if check2:
                return Result[str](f"{plus_code[2:]} {portion_locality}")

            print(
                "info: could not determine a suitable geographical feature to use as "
                "locality for shortening. full plus code is returned.",
                file=behaviour.stderr,
            )
            return Result[str](plus_code)

        case ConversionResultTypeEnum.LATLONG:
            # return the latlong if already given a latlong
            if isinstance(query, LatlongQuery):
                return Result[str](str(query))

            # get latlong and handle result
            latlong_result = query.to_lat_long_coord(geocoder=behaviour.geocoder)

            if not latlong_result:
                return Result[str]("", error=latlong_result.error)

            if behaviour.debug:
                print(f"debug: {latlong_result.get()=}", file=behaviour.stderr)

            # perform operation
            return Result[str](str(latlong_result.get()))

        case _:
            return Result[str](
                "", error=f"unknown conversion result type '{behaviour.convert_to_type}'"
            )


# command-line entry


def cli() -> int:
    """command-line entry point, returns an exit code int"""

    behaviour = handle_args()

    # handle arguments and print version header
    print(
        f"surplus version {'.'.join([str(v) for v in VERSION])}{VERSION_SUFFIX}"
        + (", debug mode" if behaviour.debug else "")
        + (
            (
                f" ({BUILD_COMMIT[:10]}@{BUILD_BRANCH}, "
                f'{BUILD_DATETIME.strftime("%a %d %b %Y %H:%M:%S %z")})'
            )
            if behaviour.debug or behaviour.version_header
            else ""
        ),
        file=behaviour.stdout if behaviour.version_header else behaviour.stderr,
    )

    if behaviour.version_header:
        sysexit(0)

    if behaviour.show_user_agent:
        print(
            generate_fingerprinted_user_agent().get(),
            file=behaviour.stdout,
        )
        sysexit(0)

    # parse query and handle result
    query = parse_query(behaviour=behaviour)

    if behaviour.debug:
        print(f"debug: cli: {query=}", file=behaviour.stderr)

    if not query:
        print(f"error: {query.cry(string=not behaviour.debug)}", file=behaviour.stderr)
        return -1

    # run surplus
    text = surplus(
        query=query.get(),
        behaviour=behaviour,
    )

    # handle and display surplus result
    if not text:
        print(f"error: {text.cry(string=not behaviour.debug)}", file=behaviour.stderr)
        return -2

    print(text.get(), file=behaviour.stdout)
    return 0


if __name__ == "__main__":
    sysexit(cli())
