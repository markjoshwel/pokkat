abbreviated MDF, or the meadow Documentation String Format

## the format

why another one? it's really just for me, but I think it's ~~a good~~ an okay-ish format

- it's easy and somewhat intuitive to read and write, especially because it's just plaintext
- it closely follows python syntax where it should, which includes type annotations

**a bonus:** it works:
- okay-ish on PyCham
- decent-ish on Zed
- slightly better on Visual Code

the format goes generally like:

```text
short one line description

more detailed description if needed

(attributes OR arguments):
    `python variable declaration syntax`
        description of the attribute

methods:
    `python function signature, including ALL arguments and type hints/return type (if available)`
        description of the method

[
returns: `return type`
    description of the return value
]

[
raises: `singular exception class`
    description of the exception(s) raised
OR
raises:
    `exception class 1`
        description 1
    `exception class 2`
        description 2
    ...
]

[
usage:
    ```py
    ...
    ```
]
```

sections in square blocks (`[]`) are optional as:

- `return None`s can omit the `return` segment
- pure, unfailing functions can omit the `raises` segment

any other sections will just be parsed as-is, so there's no stopping you from adding an `example:`
section (but cross-ide compatibility is finicky, especially with pycharm)

## guidelines when writing docstrings

### what to care about

- use the latest/most succinct forms of syntax, so even if a codebase is for Python 3.9, unions and optionals should look like `optional_argument: T | None = None`

- private fields (`_example` or `__example`) are not to be docume nted, instead you do you on how you tackle that

- externally imported/third party classes should be referenced in full except for function signatures:

    ```python
    class ThirdPartyExample(Exception):
        """
        blah blah
    
        attributes:
            `field_day: external.ExternalClass`  <- full class path
              blah blah
    
        methods:
            `def __init__(self, field_day: ExternalClass) -> None: ...`  <- note: class name
                blah blah
        """
    ```

- if having a singular docstring for overloads, use variable declaration syntaxes that make sense

    ```python
    @overload
    def get_field(
        self,
    ) -> object: ...
    
    @overload
    def get_field(
        self,
        default: DefaultT,
    ) -> Union[object, DefaultT]: ...
    
    def get_field(
        self,
        default: object = None,
    ) -> object:
        """
        ...
    
        arguments:
            `default: object | None = None` <- note: technically mismatches, but works for the overload scenario
                ...
    
        returns: `object`
        """
        ...
    ```

### when to not care:

1. classes inherited for the sake of namespacing:

    ```python
    class TomlanticException(Exception):
        """base exception class for all tomlantic errors"""
        pass
    ```

2. return descriptions when its painfully obvious
from reading pretext

    ```python
    def difference_between_document(
        self, incoming_document: TOMLDocument
    ) -> Difference:
        """
        returns a `tomlantic.Difference` namedtuple object of the incoming and
        outgoing fields that were changed between the model and the comparison document
    
        arguments:
            `incoming_document: tomlkit.TOMLDocument`
    
        returns: `tomlantic.Difference`
        """
        ...
    ```

## examples:

```python
class Result(NamedTuple, Generic[ResultType]):
    """
    typing.NamedTuple representing a result for safe value retrieval

    attributes:
        `value: ResultType`
            value to return or fallback value if erroneous
        `error: BaseException | None = None`
            exception if any

    methods:
        `def __bool__(self) -> bool: ...`
            boolean comparison for truthiness-based exception safety
        `def get(self) -> ResultType: ...`
            method that raises or returns an error if the Result is erroneous
        `def cry(self, string: bool = False) -> str: ...`
            method that returns the result value or raises an error
    """

    value: ResultType
    error: BaseException | None = None

    def __bool__(self) -> bool:
        """
        boolean comparison for truthiness-based exception safety
        
        returns: `bool`
            that returns True if `self.error` is not None
        """
        ...

    def cry(self, string: bool = False) -> str: ...  # noqa: FBT001, FBT002
        """
        raises or returns an error if the Result is erroneous

        arguments:
            `string: bool = False`
                if `self.error` is an Exception, returns it as a string error message
        
        returns: `str`
            returns `self.error` if it is a string, or returns an empty string if
            `self.error` is None
        """
        ...

    def get(self) -> ResultType:
        """
        returns the result value or raises an error

        returns: `ResultType`
            returns `self.value` if `self.error` is None

        raises: `BaseException`
            if `self.error` is not None
        """
        ...
```

```python
class ModelBoundTOML(Generic[M]):
    """
    glue class for pydantic models and tomlkit documents

    attributes:
        `model: BaseModel`

    methods:
        `def __init__(self, model: type[M], document: TOMLDocument, handle_errors: bool = True) -> None: ...`
            instantiates the class with a `pydantic.BaseModel` and a `tomlkit.TOMLDocument`
        `def model_dump_toml(self) -> TOMLDocument: ...`
            dumps the model as a style-preserved `tomlkit.TOMLDocument`
        `def get_field(self, location: str | Sequence[str], default: object | None = None) -> object | None: ...`
            safely retrieve a field by it's location
        `def set_field(self, location: str | Sequence[str], value: object) -> None: ...`
            sets a field by it's location
        `def from_another_model_bound_toml(cls, model_bound_toml: ModelBoundToml[M]) -> "ModelBoundToml": ...`
             classmethod that fully initialises from the data from another ModelBoundToml

    usage:
        ```py
        # instantiate the class
        toml = ModelBoundTOML(YourModel, tomlkit.parse(...))
        # access your model with .model
        toml.model.message = "blowy red vixens fight for a quick jump"
        # dump the model back to a toml document
        toml_document = toml.model_dump_toml()
        # or to a toml string
        toml_string = toml.model_dump_toml().as_string()
        ```
    """

    def set_field(
        self,
        location: Union[str, tuple[str, ...]],
        value: object,
        handle_errors: bool = True,
    ) -> None:
        """
        sets a field by it's location.
        
        not recommended for general use due to a lack of type safety, but useful when
        setting fields programatically

        will handle `pydantic.ValidationError` into more toml-friendly error messages.
        set `handle_errors` to `False` to raise the original `pydantic.ValidationError`

        arguments:
            `location: Union[str, tuple[str, ...]]`
                dot-separated location of the field to set
            `value: object`
                value to set at the specified location
            `handle_errors: bool = True`
                whether to convert pydantic ValidationErrors to tomlantic errors

        raises:
            `AttributeError`
                if the field does not exist
            `tomlantic.TOMLValidationError`
                if the document does not validate with the model
            `pydantic.ValidationError`
                if the document does not validate with the model and `handle_errors` is `False`
        """
```